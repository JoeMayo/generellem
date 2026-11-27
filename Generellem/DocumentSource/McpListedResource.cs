namespace Generellem.DocumentSource;

/// <summary>
/// One resource from MCP <c>resources/list</c>. Resource templates are not listed in this phase.
/// </summary>
public sealed class McpListedResource
{
    /// <summary>
    /// Resource URI. This is the path half of <c>mcp:{name}@{uri}</c>.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Display name from the server. Not used in <see cref="DocumentInfo.DocumentReference"/>.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Server-supplied description. Copied to <see cref="DocumentInfo.PathDescription"/>.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// MIME type from the list entry. Used for a blob when the read content omits a MIME type.
    /// </summary>
    public string? MimeType { get; init; }
}
