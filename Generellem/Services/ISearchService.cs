using Generellem.Embedding;

namespace Generellem.Services;

/// <summary>
/// Vector index used by ingestion and query.
/// </summary>
/// <remarks>
/// InMemory, Sqlite, Qdrant, and Azure AI Search each implement every method.
/// The interface stays in one piece so a backend can be swapped through DI
/// without callers catching <see cref="NotImplementedException"/>.
/// </remarks>
public interface ISearchService
{
    Task CreateIndexAsync(CancellationToken cancelToken);
    Task DeleteAllAsync(int companyId, CancellationToken cancellationToken);
    Task DeleteDocumentReferencesAsync(List<string> documentReferencesToDelete, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferencesAsync(string sourceReference, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken);
    Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken);
    Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken);
    Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken);
}