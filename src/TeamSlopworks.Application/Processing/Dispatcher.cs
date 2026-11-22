using TeamSlopworks.Application.Tools;
using TeamSlopworks.Domain;

namespace TeamSlopworks.Application.Processing;

public delegate Task<ToolResult<JobResult>> JobPlan(ToolInvoker tools, CancellationToken cancellationToken);

public sealed class Dispatcher(IToolRegistry registry)
{
    private static readonly JobFailure PlanFailed = new(
        "processing_failed", "The job plan failed during execution.");

    private readonly ToolInvoker _tools = new(registry);

    private static readonly JobFailure Interrupted = new(
        "processing_interrupted", "Job processing was interrupted.");

    public async Task RunAsync(Job job, JobPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();
        job.Start();

        ToolResult<JobResult> result;
        try
        {
            result = await plan(_tools, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Fail(Interrupted);
            throw;
        }
        catch (Exception)
        {
            job.Fail(PlanFailed);
            return;
        }

        switch (result)
        {
            case ToolResult<JobResult>.Success { Value: { } value }:
                job.Complete(value);
                break;
            case ToolResult<JobResult>.Failure { Error: { } error }:
                job.Fail(ToJobFailure(error));
                break;
            default:
                job.Fail(PlanFailed);
                break;
        }
    }

    private static JobFailure ToJobFailure(ToolError error)
    {
        if (string.IsNullOrWhiteSpace(error.Code) || string.IsNullOrWhiteSpace(error.Message))
        {
            return PlanFailed;
        }

        // JobFailure.code in OpenAPI is narrower than the set of tool error codes.
        return error.Code switch
        {
            "ocr_timeout" or "ocr_invalid_response" or "ocr_unavailable" or "unreadable_document"
                or "processing_interrupted" or "processing_failed" => new JobFailure(error.Code, error.Message),
            _ => new JobFailure(PlanFailed.Code, error.Message) { ToolCode = error.Code }
        };
    }

    // Run before starting workers, with jobs left behind by the previous process.
    public static void RecoverInterruptedJobs(IEnumerable<Job> jobs)
    {
        foreach (var job in jobs)
        {
            if (job.Status == JobStatus.Running)
            {
                job.Fail(Interrupted);
            }
        }
    }
}
