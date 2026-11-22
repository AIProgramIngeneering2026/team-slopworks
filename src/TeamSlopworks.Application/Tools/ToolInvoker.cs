namespace TeamSlopworks.Application.Tools;

public sealed class ToolInvoker
{
    private readonly IToolRegistry _registry;

    internal ToolInvoker(IToolRegistry registry) => _registry = registry;

    public async Task<ToolResult<TOutput>> InvokeAsync<TInput, TOutput>(
        string name, TInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_registry.TryGet(name, out var tool))
        {
            return new ToolResult<TOutput>.Failure(
                new ToolError("unknown_operation", $"Unknown operation '{name}'."));
        }

        if (tool is not ITool<TInput, TOutput> typedTool)
        {
            return new ToolResult<TOutput>.Failure(new ToolError(
                "tool_contract_mismatch", $"Operation '{name}' does not match the planned input and output types."));
        }

        try
        {
            return await typedTool.ExecuteAsync(input, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new ToolResult<TOutput>.Failure(
                new ToolError("processing_failed", $"Operation '{name}' failed during execution."));
        }
    }
}
