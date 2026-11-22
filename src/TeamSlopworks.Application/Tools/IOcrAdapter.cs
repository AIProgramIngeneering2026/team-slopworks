namespace TeamSlopworks.Application.Tools;

public interface IOcrAdapter
{
    Task<ToolResult<string>> RecognizeAsync(
        Stream pdfStream,
        string language,
        CancellationToken cancellationToken);
}
