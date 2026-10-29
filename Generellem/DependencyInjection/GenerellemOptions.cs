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
    /// Maximum characters per text chunk.
    /// </summary>
    public int ChunkSize { get; set; } = 5000;

    /// <summary>
    /// Characters repeated at the boundary between adjacent chunks.
    /// Must be zero or greater and less than <see cref="ChunkSize"/>.
    /// </summary>
    public int ChunkOverlap { get; set; } = 100;

    /// <summary>
    /// Number of chunks returned from vector search.
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    /// When true, an extra LLM call rewrites the user query from chat history before retrieval.
    /// Set false to search with the raw query and skip that call. Defaults to true.
    /// </summary>
    public bool SummarizeUserIntent { get; set; } = true;

    /// <summary>
    /// How many text chunks are sent in one embedding request during ingestion.
    /// </summary>
    public const int DefaultEmbeddingBatchSize = 16;

    /// <summary>
    /// How many text chunks are sent in one embedding request during ingestion.
    /// </summary>
    public int EmbeddingBatchSize { get; set; } = DefaultEmbeddingBatchSize;

    /// <summary>
    /// Console demo run: ingest, query, or both. The <c>--mode</c> flag overrides this.
    /// </summary>
    public GenerellemRunMode RunMode { get; set; } = GenerellemRunMode.Both;

    /// <summary>
    /// LLM temperature. The useful range depends on the model.
    /// </summary>
    public float Temperature { get; set; }

    /// <summary>
    /// AppData subfolder so one Generellem host does not overwrite another's files.
    /// </summary>
    public string SubFolder { get; set; } = string.Empty;

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

    /// <summary>
    /// Orders retrieved chunks before they are added to the prompt.
    /// </summary>
    public RerankMode RerankMode { get; set; } = RerankMode.Lexical;

    /// <summary>
    /// When true, image ingestion also asks a vision model to transcribe or describe the image.
    /// Requires <see cref="VisionModel"/>.
    /// </summary>
    public bool EnableImageVision { get; set; }

    /// <summary>
    /// Vision model name. Ollama uses this model for images. Azure uses the configured chat deployment
    /// and still requires this value so vision is an explicit choice.
    /// </summary>
    public string VisionModel { get; set; } = string.Empty;
}
