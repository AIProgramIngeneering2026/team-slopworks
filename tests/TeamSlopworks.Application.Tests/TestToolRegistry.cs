using System.Diagnostics.CodeAnalysis;
using TeamSlopworks.Application.Tools;

namespace TeamSlopworks.Application.Tests;

/// <summary>In-memory registry for pipeline tests.</summary>
public sealed class TestToolRegistry(params ITool[] tools) : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = tools.ToDictionary(tool => tool.Name);

    public bool TryGet(string name, [NotNullWhen(true)] out ITool? tool) =>
        _tools.TryGetValue(name, out tool);
}
