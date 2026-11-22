namespace TeamSlopworks.Domain;

public enum JobStatus
{
    Created,
    Running,
    Done,
    Failed
}

public sealed record JobResult(string Text, long WordCount, long CharacterCount);

public sealed record JobFailure(string Code, string Detail);

public sealed record JobState(JobStatus Status, JobResult? Result = null, string? ErrorReason = null)
{
    public JobFailure? Failure { get; init; }
}

public sealed class Job
{
    private readonly object _transitionLock = new();
    private volatile JobState _snapshot = new(JobStatus.Created);

    // Capture State once when reading status and outcome together.
    public JobState State => _snapshot;

    public JobStatus Status => State.Status;
    public JobResult? Result => State.Result;
    public JobFailure? Failure => State.Failure;
    public string? ErrorReason => State.ErrorReason;

    public void Start() => Transition(JobStatus.Created, new JobState(JobStatus.Running));

    public void Complete(JobResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Transition(JobStatus.Running, new JobState(JobStatus.Done, Result: result));
    }

    public void Fail(string errorReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorReason);
        Fail(new JobFailure("processing_failed", errorReason));
    }

    public void Fail(JobFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Detail);
        Transition(JobStatus.Running, new JobState(JobStatus.Failed, ErrorReason: failure.Detail)
        {
            Failure = failure
        });
    }

    private void Transition(JobStatus expected, JobState next)
    {
        lock (_transitionLock)
        {
            if (_snapshot.Status != expected)
            {
                throw new InvalidJobTransitionException(_snapshot.Status, next.Status);
            }

            _snapshot = next;
        }
    }
}
