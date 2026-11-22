using TeamSlopworks.Application.Tools;
using TeamSlopworks.Domain;

namespace TeamSlopworks.Application.Processing;

public static class DocumentProcessingPlan
{
    public static JobPlan Create(
        Func<CancellationToken, ValueTask<Stream>> openPdf,
        string language,
        string ocrOperation = "ocr",
        string statisticsOperation = "statistics")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ocrOperation);
        ArgumentException.ThrowIfNullOrWhiteSpace(statisticsOperation);
        ArgumentNullException.ThrowIfNull(openPdf);

        return async (tools, cancellationToken) =>
        {
            var recognized = await RecognizeAsync(tools, cancellationToken);
            if (recognized is ToolResult<string>.Failure { Error.Code: "ocr_timeout" })
            {
                recognized = await RecognizeAsync(tools, cancellationToken);
            }

            if (recognized is ToolResult<string>.Failure ocrFailure)
            {
                return new ToolResult<JobResult>.Failure(ocrFailure.Error);
            }

            var text = ((ToolResult<string>.Success)recognized).Value;
            var statistics = await tools.InvokeAsync<StatisticsToolInput, StatisticsToolOutput>(
                statisticsOperation, new StatisticsToolInput(text), cancellationToken);

            if (statistics is ToolResult<StatisticsToolOutput>.Failure statisticsFailure)
            {
                return new ToolResult<JobResult>.Failure(statisticsFailure.Error);
            }

            var counts = ((ToolResult<StatisticsToolOutput>.Success)statistics).Value;
            return new ToolResult<JobResult>.Success(new JobResult(text, counts.WordCount, counts.CharacterCount));
        };

        async Task<ToolResult<string>> RecognizeAsync(ToolInvoker tools, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var pdf = await openPdf(cancellationToken);
            return await tools.InvokeAsync<OcrToolInput, string>(
                ocrOperation, new OcrToolInput(pdf, language), cancellationToken);
        }
    }
}
