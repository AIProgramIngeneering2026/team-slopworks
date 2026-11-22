namespace TeamSlopworks.Application.Tools;

public sealed class StatisticsTool : ITool<StatisticsToolInput, StatisticsToolOutput>
{
    public const string ToolName = "statistics";

    public string Name => ToolName;

    public Task<ToolResult<StatisticsToolOutput>> ExecuteAsync(
        StatisticsToolInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (input is null)
        {
            return Failure("invalid_parameters", "Field 'input' is required.");
        }

        if (input.Text is null)
        {
            return Failure("invalid_parameters", "Field 'Text' is required.");
        }

        var wordCount = input.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).LongLength;
        var characterCount = input.Text.EnumerateRunes().LongCount();

        return Task.FromResult<ToolResult<StatisticsToolOutput>>(
            new ToolResult<StatisticsToolOutput>.Success(
                new StatisticsToolOutput(wordCount, characterCount)));
    }

    private static Task<ToolResult<StatisticsToolOutput>> Failure(string code, string message) =>
        Task.FromResult<ToolResult<StatisticsToolOutput>>(
            new ToolResult<StatisticsToolOutput>.Failure(new ToolError(code, message)));
}
