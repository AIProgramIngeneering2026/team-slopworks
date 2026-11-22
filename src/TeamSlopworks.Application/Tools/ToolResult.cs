namespace TeamSlopworks.Application.Tools;

public sealed record ToolError(string Code, string Message);

public abstract record ToolResult<T>
{
    private ToolResult() { }

    public sealed record Success(T Value) : ToolResult<T>;

    public sealed record Failure(ToolError Error) : ToolResult<T>;
}
