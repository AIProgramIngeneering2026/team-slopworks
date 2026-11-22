namespace TeamSlopworks.Domain;

public enum JobStatus
{
    Created,
    Running,
    Done,
    Failed
}

public sealed record JobResult(string Text, long WordCount, long CharacterCount);

public sealed class Job
{
    private readonly object _transitionLock = new();
    private volatile Snapshot _snapshot = new(JobStatus.Created);

    public JobStatus Status => _snapshot.Status;
    public JobResult? Result => _snapshot.Result;
    public string? ErrorReason => _snapshot.ErrorReason;

    public void Start() => Transition(JobStatus.Created, new Snapshot(JobStatus.Running));

    public void Complete(JobResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Transition(JobStatus.Running, new Snapshot(JobStatus.Done, Result: result));
    }

    public void Fail(string errorReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorReason);
        Transition(JobStatus.Running, new Snapshot(JobStatus.Failed, ErrorReason: errorReason));
    }

    private void Transition(JobStatus expected, Snapshot next)
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

    private sealed record Snapshot(
        JobStatus Status,
        JobResult? Result = null,
        string? ErrorReason = null);
}
