using TeamSlopworks.Application.Tools;

namespace TeamSlopworks.Application.Tests;

public sealed class StatisticsToolTests
{
    private readonly StatisticsTool _tool = new();

    [Fact]
    public void NameIsStatistics()
    {
        Assert.Equal("statistics", _tool.Name);
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("   \t\n", 0, 5)]
    [InlineData("Привет мир", 2, 10)]
    [InlineData("one\ttwo\u00A0three", 3, 13)]
    public async Task CountsWordsAndUnicodeCodePoints(string text, long words, long characters)
    {
        var result = await _tool.ExecuteAsync(new StatisticsToolInput(text), CancellationToken.None);

        var success = Assert.IsType<ToolResult<StatisticsToolOutput>.Success>(result);
        Assert.Equal(words, success.Value.WordCount);
        Assert.Equal(characters, success.Value.CharacterCount);
    }

    [Fact]
    public async Task SurrogatePairCountsAsOneCharacter()
    {
        var result = await _tool.ExecuteAsync(new StatisticsToolInput("a\U0001F600b"), CancellationToken.None);

        var success = Assert.IsType<ToolResult<StatisticsToolOutput>.Success>(result);
        Assert.Equal(1, success.Value.WordCount);
        Assert.Equal(3, success.Value.CharacterCount);
    }

    [Fact]
    public async Task MissingTextReturnsErrorWithFieldName()
    {
        var result = await _tool.ExecuteAsync(new StatisticsToolInput(null!), CancellationToken.None);

        var failure = Assert.IsType<ToolResult<StatisticsToolOutput>.Failure>(result);
        Assert.Equal("invalid_parameters", failure.Error.Code);
        Assert.Contains("Text", failure.Error.Message);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _tool.ExecuteAsync(new StatisticsToolInput("text"), cancellation.Token));
    }
}
