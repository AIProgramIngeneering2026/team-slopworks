using TeamSlopworks.Application.Tools;

namespace TeamSlopworks.Application.Tests;

public sealed class OcrToolTests
{
    private readonly OcrTool _tool = new(new TestOcrAdapter("Привет мир"));

    [Fact]
    public void NameIsOcr()
    {
        Assert.Equal("ocr", _tool.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("de")]
    [InlineData("RU")]
    public async Task UnsupportedLanguageReturnsErrorNamingTheField(string? language)
    {
        var result = await _tool.ExecuteAsync(
            new OcrToolInput(new MemoryStream(), language!), CancellationToken.None);

        var failure = Assert.IsType<ToolResult<string>.Failure>(result);
        Assert.Equal("invalid_parameters", failure.Error.Code);
        Assert.Contains("Language", failure.Error.Message);
    }

    [Fact]
    public async Task MissingPdfStreamReturnsErrorNamingTheField()
    {
        var result = await _tool.ExecuteAsync(new OcrToolInput(null!, "ru"), CancellationToken.None);

        var failure = Assert.IsType<ToolResult<string>.Failure>(result);
        Assert.Equal("invalid_parameters", failure.Error.Code);
        Assert.Contains("PdfStream", failure.Error.Message);
    }

    [Fact]
    public async Task PassesStreamAndLanguageToAdapterWithoutClosingTheStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        Stream? receivedStream = null;
        string? receivedLanguage = null;
        var tool = new OcrTool(new LambdaOcrAdapter((pdf, language, _) =>
        {
            receivedStream = pdf;
            receivedLanguage = language;
            return Task.FromResult<ToolResult<string>>(new ToolResult<string>.Success("text"));
        }));

        var result = await tool.ExecuteAsync(new OcrToolInput(stream, "en"), CancellationToken.None);

        Assert.Same(stream, receivedStream);
        Assert.Equal("en", receivedLanguage);
        Assert.True(stream.CanRead);
        Assert.Equal("text", Assert.IsType<ToolResult<string>.Success>(result).Value);
    }

    [Theory]
    [InlineData("ocr_timeout")]
    [InlineData("ocr_invalid_response")]
    [InlineData("ocr_unavailable")]
    [InlineData("unreadable_document")]
    public async Task AdapterFailuresArePassedThrough(string code)
    {
        var tool = new OcrTool(new LambdaOcrAdapter((_, _, _) =>
            Task.FromResult<ToolResult<string>>(
                new ToolResult<string>.Failure(new ToolError(code, "OCR rejected the request.")))));

        var result = await tool.ExecuteAsync(
            new OcrToolInput(new MemoryStream(), "ru"), CancellationToken.None);

        var failure = Assert.IsType<ToolResult<string>.Failure>(result);
        Assert.Equal(code, failure.Error.Code);
    }

    [Fact]
    public async Task EmptyRecognitionIsSuccess()
    {
        var tool = new OcrTool(new LambdaOcrAdapter((_, _, _) =>
            Task.FromResult<ToolResult<string>>(new ToolResult<string>.Success(string.Empty))));

        var result = await tool.ExecuteAsync(
            new OcrToolInput(new MemoryStream(), "ru"), CancellationToken.None);

        Assert.Equal(string.Empty, Assert.IsType<ToolResult<string>.Success>(result).Value);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _tool.ExecuteAsync(new OcrToolInput(new MemoryStream(), "ru"), cancellation.Token));
    }

    private sealed class LambdaOcrAdapter(
        Func<Stream, string, CancellationToken, Task<ToolResult<string>>> recognize) : IOcrAdapter
    {
        public Task<ToolResult<string>> RecognizeAsync(
            Stream pdfStream,
            string language,
            CancellationToken cancellationToken) =>
            recognize(pdfStream, language, cancellationToken);
    }
}
