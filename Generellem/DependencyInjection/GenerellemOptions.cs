namespace Generellem.DependencyInjection;

/// <summary>
/// Configuration for Generellem hosting profiles and shared retrieval settings.
/// </summary>
public class GenerellemOptions
{
    public const string SectionName = "Generellem";

    /// <summary>
    /// Active profile (set automatically by <see cref="GenerellemServiceCollectionExtensions.AddGenerellem"/>).
    /// </summary>
    public GenerellemProfile Profile { get; set; } = GenerellemProfile.InMemory;

    /// <summary>
    /// Embedding vector size. Azure OpenAI ada-002 = 1536; nomic-embed-text = 768.
    /// </summary>
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>
    /// Number of chunks returned from vector search.
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    /// Ollama base URL (no trailing slash).
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Ollama chat model name.
    /// </summary>
    public string OllamaChatModel { get; set; } = "llama3.2";

    /// <summary>
    /// Ollama embedding model name.
    /// </summary>
    public string OllamaEmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// When using <see cref="GenerellemProfile.Ollama"/>, store vectors in SQLite instead of memory.
    /// </summary>
    public bool OllamaUseSqliteSearch { get; set; }

    /// <summary>
    /// When using <see cref="GenerellemProfile.Azure"/>, use Azure AI Search instead of Qdrant.
    /// </summary>
    public bool UseAzureSearch { get; set; }
}
