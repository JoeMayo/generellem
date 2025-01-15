using Generellem.Embedding;

namespace Generellem.Services;

public interface ISearchService
{
    Task CreateIndexAsync(CancellationToken cancelToken);
    Task DeleteAllAsync(int companyId, CancellationToken cancellationToken);
    Task DeleteDocumentReferencesAsync(List<string> documentReferencesToDelete, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferencesAsync(string sourceReference, CancellationToken cancellationToken);
    Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken);
    Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken);
    Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken);
}