using TeamSlopworks.Application.Processing;
using TeamSlopworks.Application.Tools;
using TeamSlopworks.Domain;

namespace TeamSlopworks.Application.Tests;

public sealed class ToolPipelineTests
{
    [Fact]
    public async Task OcrAndStatisticsToolsProduceJobResultThroughDispatcher()
    {
        const string text = "Привет мир";
        var dispatcher = new Dispatcher(new TestToolRegistry(
            new OcrTool(new TestOcrAdapter(text)),
            new StatisticsTool()));
        var job = new Job();

        await dispatcher.RunAsync(job, DocumentProcessingPlan.Create(
            _ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3])), "ru"));

        Assert.Equal(JobStatus.Done, job.Status);
        Assert.Equal(new JobResult(text, 2, 10), job.Result);
        Assert.Null(job.Failure);
    }

    [Fact]
    public async Task FailedOcrLeavesJobWithoutResult()
    {
        var adapter = new FailingOcrAdapter(
            new ToolError("ocr_unavailable", "OCR service is unavailable."));
        var dispatcher = new Dispatcher(new TestToolRegistry(
            new OcrTool(adapter),
            new StatisticsTool()));
        var job = new Job();

        await dispatcher.RunAsync(job, DocumentProcessingPlan.Create(
            _ => ValueTask.FromResult<Stream>(new MemoryStream([1])), "ru"));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("ocr_unavailable", job.Failure?.Code);
        Assert.Null(job.Result);
    }

    private sealed class FailingOcrAdapter(ToolError error) : IOcrAdapter
    {
        public Task<ToolResult<string>> RecognizeAsync(
            Stream pdfStream,
            string language,
            CancellationToken cancellationToken) =>
            Task.FromResult<ToolResult<string>>(new ToolResult<string>.Failure(error));
    }
}
