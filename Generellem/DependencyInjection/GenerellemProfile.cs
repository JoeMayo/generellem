namespace Generellem.DependencyInjection;

/// <summary>
/// Hosting profile that selects search, embedding, and LLM implementations.
/// </summary>
public enum GenerellemProfile
{
    /// <summary>
    /// Zero infrastructure: in-memory vectors, deterministic embeddings, local extractive LLM.
    /// </summary>
    InMemory,

    /// <summary>
    /// File-based vectors in SQLite with deterministic embeddings and local extractive LLM.
    /// </summary>
    Sqlite,

    /// <summary>
    /// Real local AI via Ollama; vectors stored in memory by default.
    /// </summary>
    Ollama,

    /// <summary>
    /// Production path: Azure OpenAI + Qdrant (existing behavior).
    /// </summary>
    Azure
}
