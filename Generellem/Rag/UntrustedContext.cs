namespace Generellem.Rag;

/// <summary>
/// Delimiters that mark retrieved document text as data, not instructions.
/// </summary>
public static class UntrustedContext
{
    public const string Start = "<<<UNTRUSTED_CONTEXT>>>";

    public const string End = "<<<END_UNTRUSTED_CONTEXT>>>";

    public static string Wrap(string body) =>
        $"{Start}\n{body}\n{End}";

    /// <summary>
    /// Removes delimiter tokens so retrieved text cannot close the context block early.
    /// </summary>
    public static string Sanitize(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        return content
            .Replace(Start, "[removed delimiter]", StringComparison.Ordinal)
            .Replace(End, "[removed delimiter]", StringComparison.Ordinal);
    }
}
