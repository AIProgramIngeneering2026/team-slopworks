namespace TeamSlopworks.Application.Tools;

public sealed class OcrTool : ITool<OcrToolInput, string>
{
    public const string ToolName = "ocr";

    private static readonly string[] SupportedLanguages = ["ru", "en"];

    private readonly IOcrAdapter _adapter;

    public OcrTool(IOcrAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
    }

    public string Name => ToolName;

    public async Task<ToolResult<string>> ExecuteAsync(
        OcrToolInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (input is null)
        {
            return Failure("invalid_parameters", "Field 'input' is required.");
        }

        if (input.PdfStream is null)
        {
            return Failure("invalid_parameters", "Field 'PdfStream' is required.");
        }

        if (!SupportedLanguages.Contains(input.Language))
        {
            return Failure("invalid_parameters", "Field 'Language' must be 'ru' or 'en'.");
        }

        return await _adapter.RecognizeAsync(input.PdfStream, input.Language, cancellationToken);
    }

    private static Task<ToolResult<string>> Failure(string code, string message) =>
        Task.FromResult<ToolResult<string>>(
            new ToolResult<string>.Failure(new ToolError(code, message)));
}
