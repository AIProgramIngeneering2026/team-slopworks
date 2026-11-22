using System.Diagnostics.CodeAnalysis;

namespace TeamSlopworks.Application.Tools;

public interface IToolRegistry
{
    bool TryGet(string name, [NotNullWhen(true)] out ITool? tool);
}
