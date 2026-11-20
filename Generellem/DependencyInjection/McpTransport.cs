namespace Generellem.DependencyInjection;

/// <summary>
/// How Generellem reaches an MCP server.
/// </summary>
public enum McpTransport
{
    /// <summary>
    /// No transport was configured. Startup rejects this.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Launch a local process and speak MCP over stdin and stdout.
    /// </summary>
    Stdio = 1,

    /// <summary>
    /// Connect to an MCP server at an HTTP URL.
    /// </summary>
    Http = 2
}
