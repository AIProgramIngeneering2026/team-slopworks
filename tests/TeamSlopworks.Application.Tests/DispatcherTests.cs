using System.Diagnostics.CodeAnalysis;
using TeamSlopworks.Application.Processing;
using TeamSlopworks.Application.Tools;
using TeamSlopworks.Domain;

namespace TeamSlopworks.Application.Tests;

public sealed class DispatcherTests
{
    private const string Ocr = "ocr";
    private const string Statistics = "statistics";

    [Fact]
    public async Task DocumentPlanPassesTextToStatisticsAndCompletesJob()
    {
        const string text = "Привет мир";
        var job = new Job();
        var stream = new MemoryStream([1, 2]);
        var ocr = new StubTool<OcrToolInput, string>(Ocr, (input, _) =>
        {
            Assert.Equal(JobStatus.Running, job.Status);
            Assert.Same(stream, input.PdfStream);
            Assert.Equal("ru", input.Language);
            return Task.FromResult<ToolResult<string>>(new ToolResult<string>.Success(text));
        });
        var statistics = new StubTool<StatisticsToolInput, StatisticsToolOutput>(Statistics, (input, _) =>
        {
            Assert.Equal(text, input.Text);
            Assert.False(stream.CanRead);
            return Task.FromResult<ToolResult<StatisticsToolOutput>>(
                new ToolResult<StatisticsToolOutput>.Success(new StatisticsToolOutput(2, 10)));
        });
        var dispatcher = new Dispatcher(new StubRegistry(ocr, statistics));

        await dispatcher.RunAsync(job, CreateDocumentPlan(_ => ValueTask.FromResult<Stream>(stream)));

        Assert.Equal(JobStatus.Done, job.Status);
        Assert.Equal(new JobResult(text, 2, 10), job.Result);
        Assert.Null(job.Failure);
    }

    [Fact]
    public async Task UnknownOperationFailsJobAndNamesTheMissingTool()
    {
        var job = new Job();
        var dispatcher = new Dispatcher(new StubRegistry());

        await dispatcher.RunAsync(job, CreateDocumentPlan());

        var error = Assert.IsType<JobFailure>(job.Failure);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("unknown_operation", error.Code);
        Assert.Contains(Ocr, error.Detail);
        Assert.Null(job.Result);
    }

    [Fact]
    public async Task ToolFailurePreservesCodeAndDetailWithoutRetrying()
    {
        var job = new Job();
        var error = new ToolError("invalid_parameters", "Language must be 'ru' or 'en'.");
        var attempts = 0;
        var ocr = new StubTool<OcrToolInput, string>(Ocr, (_, _) =>
        {
            attempts++;
            return Task.FromResult<ToolResult<string>>(new ToolResult<string>.Failure(error));
        });
        var dispatcher = new Dispatcher(new StubRegistry(ocr));

        await dispatcher.RunAsync(job, CreateDocumentPlan());

        var state = job.State;
        Assert.Equal(JobStatus.Failed, state.Status);
        Assert.Equal(new JobFailure(error.Code, error.Message), state.Failure);
        Assert.Equal(error.Message, state.ErrorReason);
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OcrTimeoutRetriesOnceWithAFreshPdf(bool secondAttemptSucceeds)
    {
        var job = new Job();
        var inputs = new List<MemoryStream>();
        var attempts = 0;
        var statisticsCalls = 0;
        var timeout = new ToolError("ocr_timeout", "OCR did not respond in time.");
        var ocr = new StubTool<OcrToolInput, string>(Ocr, (input, _) =>
        {
            attempts++;
            Assert.Equal(JobStatus.Running, job.Status);
            Assert.Equal(0, input.PdfStream.Position);
            Assert.Equal(42, input.PdfStream.ReadByte());
            ToolResult<string> result = attempts == 2 && secondAttemptSucceeds
                ? new ToolResult<string>.Success("text")
                : new ToolResult<string>.Failure(timeout);
            return Task.FromResult(result);
        });
        var statistics = new StubTool<StatisticsToolInput, StatisticsToolOutput>(Statistics, (_, _) =>
        {
            statisticsCalls++;
            return Task.FromResult<ToolResult<StatisticsToolOutput>>(
                new ToolResult<StatisticsToolOutput>.Success(new StatisticsToolOutput(1, 4)));
        });
        var dispatcher = new Dispatcher(new StubRegistry(ocr, statistics));
        var plan = CreateDocumentPlan(_ =>
        {
            if (inputs.Count > 0)
            {
                Assert.False(inputs[^1].CanRead);
            }

            var input = new MemoryStream([42]);
            inputs.Add(input);
            return ValueTask.FromResult<Stream>(input);
        });

        await dispatcher.RunAsync(job, plan);

        Assert.Equal(2, attempts);
        Assert.Equal(2, inputs.Count);
        Assert.All(inputs, input => Assert.False(input.CanRead));
        Assert.Equal(secondAttemptSucceeds ? 1 : 0, statisticsCalls);
        Assert.Equal(secondAttemptSucceeds ? JobStatus.Done : JobStatus.Failed, job.Status);
        Assert.Equal(secondAttemptSucceeds ? new JobResult("text", 1, 4) : null, job.Result);
        Assert.Equal(secondAttemptSucceeds ? null : new JobFailure(timeout.Code, timeout.Message), job.Failure);
    }

    [Fact]
    public async Task ToolExceptionStopsThePlanBeforeStatistics()
    {
        var job = new Job();
        var ocr = new StubTool<OcrToolInput, string>(Ocr, (_, _) =>
            Task.FromException<ToolResult<string>>(new InvalidOperationException("OCR failed")));
        var statisticsCalls = 0;
        var statistics = new StubTool<StatisticsToolInput, StatisticsToolOutput>(Statistics, (_, _) =>
        {
            statisticsCalls++;
            return Task.FromResult<ToolResult<StatisticsToolOutput>>(
                new ToolResult<StatisticsToolOutput>.Success(new StatisticsToolOutput(0, 0)));
        });
        var dispatcher = new Dispatcher(new StubRegistry(ocr, statistics));

        await dispatcher.RunAsync(job, CreateDocumentPlan());

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("processing_failed", job.Failure?.Code);
        Assert.Null(job.Result);
        Assert.Equal(0, statisticsCalls);
    }

    [Fact]
    public async Task DifferentPlanCanRunAnotherRegisteredTool()
    {
        const string operation = "format-number";
        var job = new Job();
        var tool = new StubTool<int, string>(operation, (input, _) =>
            Task.FromResult<ToolResult<string>>(new ToolResult<string>.Success($"Number {input}")));
        var dispatcher = new Dispatcher(new StubRegistry(tool));

        await dispatcher.RunAsync(job, async (tools, token) =>
        {
            var result = await tools.InvokeAsync<int, string>(operation, 7, token);
            var text = Assert.IsType<ToolResult<string>.Success>(result).Value;
            return new ToolResult<JobResult>.Success(new JobResult(text, 2, 8));
        });

        Assert.Equal(JobStatus.Done, job.Status);
        Assert.Equal("Number 7", job.Result?.Text);
    }

    [Fact]
    public async Task RejectedStartDoesNotExecuteThePlan()
    {
        var job = new Job();
        job.Start();
        var dispatcher = new Dispatcher(new StubRegistry());
        var called = false;

        await Assert.ThrowsAsync<InvalidJobTransitionException>(() => dispatcher.RunAsync(job, (_, _) =>
        {
            called = true;
            return Task.FromResult<ToolResult<JobResult>>(
                new ToolResult<JobResult>.Success(new JobResult("", 0, 0)));
        }));

        Assert.False(called);
        Assert.Equal(JobStatus.Running, job.Status);
    }

    [Fact]
    public async Task InterruptedExecutionFailsJobAndPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var job = new Job();
        var dispatcher = new Dispatcher(new StubRegistry());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.RunAsync(job, (_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<ToolResult<JobResult>>(token);
        }, cancellation.Token));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("processing_interrupted", job.Failure?.Code);
    }

    [Fact]
    public void RecoveryFailsOnlyInterruptedJobs()
    {
        var pending = new Job();
        var interrupted = new Job();
        interrupted.Start();
        var completed = new Job();
        completed.Start();
        completed.Complete(new JobResult("done", 1, 4));

        Dispatcher.RecoverInterruptedJobs([pending, interrupted, completed]);

        Assert.Equal(JobStatus.Created, pending.Status);
        Assert.Equal(JobStatus.Failed, interrupted.Status);
        Assert.Equal("processing_interrupted", interrupted.Failure?.Code);
        Assert.Equal(JobStatus.Done, completed.Status);
        Assert.Equal("done", completed.Result?.Text);
    }

    private static JobPlan CreateDocumentPlan(Func<CancellationToken, ValueTask<Stream>>? openPdf = null) =>
        DocumentProcessingPlan.Create(openPdf ?? (_ => ValueTask.FromResult<Stream>(new MemoryStream())), "ru");

    private sealed class StubRegistry(params ITool[] tools) : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = tools.ToDictionary(tool => tool.Name);

        public bool TryGet(string name, [NotNullWhen(true)] out ITool? tool) => _tools.TryGetValue(name, out tool);
    }

    private sealed class StubTool<TInput, TOutput>(
        string name, Func<TInput, CancellationToken, Task<ToolResult<TOutput>>> execute) : ITool<TInput, TOutput>
    {
        public string Name => name;

        public Task<ToolResult<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken) =>
            execute(input, cancellationToken);
    }
}
