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

        Assert.Throws<InvalidOperationException>(job.Start);
        AssertSnapshot(job, JobStatus.Running);
    }

    [Fact]
    public void Z4_CannotCompleteAfterFailure()
    {
        var job = new Job();
        job.Start();
        job.Fail("ocr_failed");

        Assert.Throws<InvalidOperationException>(() => job.Complete(Result));
        AssertSnapshot(job, JobStatus.Failed, errorReason: "ocr_failed");
    }

    [Fact]
    public void Z5_CannotCompleteBeforeStarting()
    {
        var job = new Job();

        Assert.Throws<InvalidOperationException>(() => job.Complete(Result));
        AssertSnapshot(job, JobStatus.Created);
    }

    [Fact]
    public void Z6_CannotRestartAfterSuccess()
    {
        var job = new Job();
        job.Start();
        job.Complete(Result);

        Assert.Throws<InvalidOperationException>(job.Start);
        AssertSnapshot(job, JobStatus.Done, Result);
    }

    private static void AssertSnapshot(
        Job job, JobStatus status, JobResult? result = null, string? errorReason = null)
    {
        Assert.Equal(status, job.Status);
        Assert.Equal(result, job.Result);
        Assert.Equal(errorReason, job.ErrorReason);
    }
}
