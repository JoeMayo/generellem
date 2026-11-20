using System.Text.RegularExpressions;

namespace Generellem.DependencyInjection;

/// <summary>
/// One MCP server whose resources can be ingested.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Default cap on resources read from one server during an ingest.
    /// </summary>
    public const int DefaultMaxResources = 10000;

    /// <summary>
    /// Names are 1 to 64 characters: a letter or digit, then letters, digits, underscore, or hyphen.
    /// One name must not be a prefix of another. Deletion looks up documents with
    /// <c>StartsWith(Reference)</c>, and <c>mcp:notes</c> would also match <c>mcp:notes-extra</c>.
    /// </summary>
    public const string NamePatternText = "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$";

    static readonly Regex NamePattern = new(
        NamePatternText,
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Server name. Required. Unique, and not a prefix of another server name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="McpTransport.Stdio"/> or <see cref="McpTransport.Http"/>.
    /// </summary>
    public McpTransport Transport { get; set; }

    /// <summary>
    /// Process to launch. Required for <see cref="McpTransport.Stdio"/>. Forbidden for HTTP.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Arguments for <see cref="Command"/>. Stdio only.
    /// </summary>
    public List<string> Arguments { get; set; } = [];

    /// <summary>
    /// Environment variables for the stdio process. Stdio only. Values are credentials; do not log them.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Absolute http or https URI. Required for <see cref="McpTransport.Http"/>. Forbidden for stdio.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Request headers for HTTP. HTTP only. Values are credentials; do not log them.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Maximum resources read from this server. Defaults to <see cref="DefaultMaxResources"/>.
    /// </summary>
    public int MaxResources { get; set; } = DefaultMaxResources;

    /// <summary>
    /// True when <paramref name="name"/> matches <see cref="NamePatternText"/>.
    /// </summary>
    public static bool IsValidName(string? name) =>
        name is not null && NamePattern.IsMatch(name);
}
