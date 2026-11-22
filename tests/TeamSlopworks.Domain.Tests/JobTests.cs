namespace TeamSlopworks.Domain.Tests;

public sealed class JobTests
{
    private static readonly JobResult Result = new("Hello world", 2, 11);

    [Fact]
    public void StartAndCompleteStoresResult()
    {
        var job = new Job();
        AssertSnapshot(job, JobStatus.Created);

        job.Start();
        AssertSnapshot(job, JobStatus.Running);

        job.Complete(Result);
        AssertSnapshot(job, JobStatus.Done, Result);
    }

    [Fact]
    public void Z1_CannotStartWhileRunning()
    {
        var job = new Job();
        job.Start();

        Assert.Throws<InvalidJobTransitionException>(job.Start);
        AssertSnapshot(job, JobStatus.Running);
    }

    [Fact]
    public void Z4_CannotCompleteAfterFailure()
    {
        var job = new Job();
        job.Start();
        job.Fail("ocr_failed");

        Assert.Throws<InvalidJobTransitionException>(() => job.Complete(Result));
        AssertSnapshot(job, JobStatus.Failed, errorReason: "ocr_failed");
    }

    [Fact]
    public void Z5_CannotCompleteBeforeStarting()
    {
        var job = new Job();

        Assert.Throws<InvalidJobTransitionException>(() => job.Complete(Result));
        AssertSnapshot(job, JobStatus.Created);
    }

    [Fact]
    public void Z6_CannotRestartAfterSuccess()
    {
        var job = new Job();
        job.Start();
        job.Complete(Result);

        Assert.Throws<InvalidJobTransitionException>(job.Start);
        AssertSnapshot(job, JobStatus.Done, Result);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Done)]
    [InlineData(JobStatus.Failed)]
    public void CannotFailOutsideRunning(JobStatus status)
    {
        var job = new Job();
        if (status != JobStatus.Created)
            job.Start();
        if (status == JobStatus.Done)
            job.Complete(Result);
        if (status == JobStatus.Failed)
            job.Fail("ocr_failed");

        var error = Assert.Throws<InvalidJobTransitionException>(() => job.Fail("other_error"));

        Assert.Equal(status, error.CurrentStatus);
        Assert.Equal(JobStatus.Failed, error.RequestedStatus);
        AssertSnapshot(job, status, status == JobStatus.Done ? Result : null,
            status == JobStatus.Failed ? "ocr_failed" : null);
    }

    [Fact]
    public void CannotReplaceCompletedResult()
    {
        var job = new Job();
        job.Start();
        var original = new JobResult("text", 1, (long)int.MaxValue + 1);
        job.Complete(original);

        Assert.Throws<InvalidJobTransitionException>(() => job.Complete(Result));
        AssertSnapshot(job, JobStatus.Done, original);
    }

    [Fact]
    public async Task ConcurrentStartsAllowOnlyOneClaim()
    {
        var job = new Job();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Exception?> ClaimAsync() => Task.Run<Exception?>(async () =>
        {
            await start.Task;
            return Record.Exception(job.Start);
        });

        var first = ClaimAsync();
        var second = ClaimAsync();
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);

        Assert.Single(outcomes, outcome => outcome is null);
        var rejection = Assert.IsType<InvalidJobTransitionException>(
            Assert.Single(outcomes, outcome => outcome is not null));
        Assert.Equal(JobStatus.Running, rejection.CurrentStatus);
        Assert.Equal(JobStatus.Running, rejection.RequestedStatus);
        AssertSnapshot(job, JobStatus.Running);
    }

    [Fact]
    public void CapturedStateKeepsStatusAndOutcomeFromTheSameTransition()
    {
        var job = new Job();
        job.Start();
        var running = job.State;

        job.Complete(Result);

        Assert.Equal(new JobState(JobStatus.Running), running);
        Assert.Equal(new JobState(JobStatus.Done, Result), job.State);
    }

    private static void AssertSnapshot(
        Job job, JobStatus status, JobResult? result = null, string? errorReason = null)
    {
        Assert.Equal(status, job.Status);
        Assert.Equal(result, job.Result);
        Assert.Equal(errorReason, job.ErrorReason);
    }
}
