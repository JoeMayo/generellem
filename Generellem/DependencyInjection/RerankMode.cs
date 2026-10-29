namespace Generellem.DependencyInjection;

/// <summary>
/// How retrieved chunks are ordered before they are placed in the prompt.
/// </summary>
public enum RerankMode
{
    /// <summary>
    /// Keep the search order.
    /// </summary>
    None = 0,

    /// <summary>
    /// Score each chunk against the query with term overlap.
    /// </summary>
    Lexical = 1,

    /// <summary>
    /// Ask the chat model to order the chunks. Falls back to the search order when the reply cannot be parsed.
    /// </summary>
    Llm = 2
}
