namespace TeamSlopworks.Domain;

public sealed class InvalidJobTransitionException(JobStatus currentStatus, JobStatus requestedStatus)
    : InvalidOperationException($"Cannot transition job from {currentStatus} to {requestedStatus}.")
{
    public JobStatus CurrentStatus { get; } = currentStatus;
    public JobStatus RequestedStatus { get; } = requestedStatus;
}
