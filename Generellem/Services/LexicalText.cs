namespace Generellem.Services;

/// <summary>
/// Shared tokenization for keyword search, sparse vectors, and reranking.
/// </summary>
public static class LexicalText
{
    public static List<string> Tokens(string? text)
    {
        List<string> tokens = [];
        if (string.IsNullOrEmpty(text))
            return tokens;

        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && !IsTokenChar(text[i]))
                i++;

            int start = i;
            while (i < text.Length && IsTokenChar(text[i]))
                i++;

            if (i > start)
                tokens.Add(text[start..i].ToLowerInvariant());
        }

        return tokens;
    }

    /// <summary>
    /// FTS5 MATCH expression. Tokens are quoted and combined with OR.
    /// Returns empty when the text has no tokens.
    /// </summary>
    public static string ToFtsMatch(string? text)
    {
        List<string> tokens = Tokens(text);
        if (tokens.Count == 0)
            return string.Empty;

        return string.Join(" OR ", tokens.Select(token => "\"" + token.Replace("\"", string.Empty) + "\""));
    }

    public static int Overlap(string? query, string? content)
    {
        List<string> queryTokens = Tokens(query);
        if (queryTokens.Count == 0)
            return 0;

        HashSet<string> terms = queryTokens.ToHashSet(StringComparer.Ordinal);
        int score = 0;
        foreach (string token in Tokens(content))
        {
            if (terms.Contains(token))
                score++;
        }

        return score;
    }

    static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c);
}
