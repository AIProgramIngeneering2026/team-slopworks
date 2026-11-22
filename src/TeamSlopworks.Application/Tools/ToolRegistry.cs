using System.Diagnostics.CodeAnalysis;

namespace TeamSlopworks.Application.Tools;

/// <summary>
/// Provides registration and name-based lookup of tool implementations.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a tool under its name.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tool"/> is null.</exception>
    /// <exception cref="ArgumentException">The tool name is empty or already registered.</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.Name);

        if (!_tools.TryAdd(tool.Name, tool))
        {
            throw new ArgumentException(
                $"A tool with the name '{tool.Name}' is already registered.",
                nameof(tool));
        }
    }

    /// <summary>
    /// Attempts to find a registered tool by its exact name.
    /// </summary>
    /// <param name="name">The tool name to look up.</param>
    /// <param name="tool">The registered tool, or null when no tool has the name.</param>
    /// <returns>true when a tool with the name is registered, otherwise false.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out ITool? tool)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            tool = null;
            return false;
        }

        return _tools.TryGetValue(name, out tool);
    }
}
