using TeamSlopworks.Application.Tools;

namespace TeamSlopworks.Application.Tests;

/// <summary>
/// Deterministic OCR implementation for P3. It stands for the external provider
/// until the real HTTP adapter is added in P4.
/// </summary>
public sealed class TestOcrAdapter(string recognizedText = "Привет мир") : IOcrAdapter
{
    public string RecognizedText { get; } = recognizedText;

    public Task<ToolResult<string>> RecognizeAsync(
        Stream pdfStream,
        string language,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfStream);

        if (!pdfStream.CanRead)
        {
            return Task.FromResult<ToolResult<string>>(
                new ToolResult<string>.Failure(
                    new ToolError("unreadable_document", "The PDF stream is not readable.")));
        }

        // The stream belongs to Dispatcher: do not dispose or seek it here.
        return Task.FromResult<ToolResult<string>>(
            new ToolResult<string>.Success(RecognizedText));
    }
}
