using TeamSlopworks.Application.Tools;

namespace TeamSlopworks.Application.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void TryGetReturnsRegisteredTool()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("ocr");
        registry.Register(tool);

        var found = registry.TryGet("ocr", out var result);

        Assert.True(found);
        Assert.Same(tool, result);
    }

    [Fact]
    public void TryGetReturnsFalseForUnknownOperation()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("ocr"));

        var found = registry.TryGet("statistics", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetMatchesNameExactly()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("ocr"));

        var found = registry.TryGet("OCR", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryGetReturnsFalseForMissingName(string? name)
    {
        var registry = new ToolRegistry();

        var found = registry.TryGet(name!, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void RegisterRejectsDuplicateName()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("ocr"));

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeTool("ocr")));
    }

    [Fact]
    public void RegisterRejectsToolWithoutName()
    {
        var registry = new ToolRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeTool(" ")));
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    private sealed class FakeTool(string name) : ITool
    {
        public string Name { get; } = name;
    }
}
