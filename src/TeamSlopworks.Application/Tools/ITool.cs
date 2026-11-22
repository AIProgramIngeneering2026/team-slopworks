namespace TeamSlopworks.Application.Tools;

public interface ITool
{
    string Name { get; }
}

public interface ITool<TInput, TOutput> : ITool
{
    Task<ToolResult<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
