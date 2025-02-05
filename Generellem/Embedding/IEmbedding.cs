using Generellem.Document.DocumentTypes;
using Generellem.Processors;

using Polly;

namespace Generellem.Embedding;

public interface IEmbedding
{
    ResiliencePipeline Pipeline { get; set; }

    /// <summary>
    /// Takes a raw document, turns it into text, and creates an embedding.
    /// </summary>
    /// <param name="fullText">Document to embed.</param>
    /// <param name="docType">An <see cref="IDocumentType"/> for turning the document into text.</param>
    /// <param name="fileName">Name of file in embedding.</param>
    /// <param name="progress">Reports progress.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="List{T}"/> of <see cref="TextChunk"/>, which holds both the real and embedded content.</returns>
    Task<List<TextChunk>> EmbedAsync(string fullText, IDocumentType docType, string fileName, IProgress<IngestionProgress> progress, CancellationToken cancellationToken);
    Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken);
}
