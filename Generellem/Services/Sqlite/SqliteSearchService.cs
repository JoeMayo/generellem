using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Repository;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Generellem.Services.Sqlite;

/// <summary>
/// Brute-force cosine search over vectors stored in SQLite (no native extension required).
/// </summary>
public class SqliteSearchService(GenerellemContext ctx, IOptions<GenerellemOptions> options) : ISearchService
{
    readonly GenerellemOptions options = options.Value;

    public Task CreateIndexAsync(CancellationToken cancelToken) => Task.CompletedTask;

    public async Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        await ctx.VectorChunks.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteDocumentReferencesAsync(List<string> documentReferencesToDelete, CancellationToken cancellationToken)
    {
        if (documentReferencesToDelete.Count == 0)
            return;

        await ctx.VectorChunks
            .Where(c => documentReferencesToDelete.Contains(c.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken)
    {
        List<VectorChunk> rows = await ctx.VectorChunks
            .AsNoTracking()
            .Where(c => c.DocumentReference == documentReference)
            .ToListAsync(cancellationToken);

        return rows.Select(ToTextChunkMetadata).ToList();
    }

    public async Task<List<TextChunk>> GetDocumentReferencesAsync(string sourceReference, CancellationToken cancellationToken)
    {
        List<VectorChunk> rows = await ctx.VectorChunks
            .AsNoTracking()
            .Where(c =>
                c.SourceReference == sourceReference ||
                (c.DocumentReference != null && c.DocumentReference.StartsWith(sourceReference)))
            .ToListAsync(cancellationToken);

        return rows.Select(ToTextChunkMetadata).ToList();
    }

    public async Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken)
    {
        List<VectorChunk> rows = await ctx.VectorChunks
            .AsNoTracking()
            .Where(c => c.DocumentReference != null && c.DocumentReference.Contains(path))
            .ToListAsync(cancellationToken);

        return rows.Select(ToTextChunkMetadata).ToList();
    }

    public async Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken)
    {
        foreach (TextChunk doc in documents)
        {
            if (string.IsNullOrWhiteSpace(doc.ID))
                doc.ID = Guid.NewGuid().ToString();

            VectorChunk? existing = await ctx.VectorChunks.FindAsync([doc.ID], cancelToken);
            if (existing is null)
            {
                ctx.VectorChunks.Add(new VectorChunk
                {
                    Id = doc.ID,
                    Content = doc.Content,
                    DocumentReference = doc.DocumentReference,
                    SourceReference = doc.SourceReference,
                    Order = doc.Order,
                    Embedding = VectorMath.ToBytes(doc.Embedding)
                });
            }
            else
            {
                existing.Content = doc.Content;
                existing.DocumentReference = doc.DocumentReference;
                existing.SourceReference = doc.SourceReference;
                existing.Order = doc.Order;
                existing.Embedding = VectorMath.ToBytes(doc.Embedding);
            }
        }

        await ctx.SaveChangesAsync(cancelToken);
    }

    public async Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken)
    {
        int topK = Math.Max(1, options.TopK);

        List<VectorChunk> rows = await ctx.VectorChunks.AsNoTracking().ToListAsync(cancelToken);

        return rows
            .Select(r => (Row: r, Score: VectorMath.CosineSimilarity(embedding.Span, VectorMath.FromBytes(r.Embedding).Span)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new TextChunk
            {
                ID = x.Row.Id,
                Content = x.Row.Content,
                DocumentReference = x.Row.DocumentReference,
                SourceReference = x.Row.SourceReference,
                Order = x.Row.Order
            })
            .ToList();
    }

    public Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken) =>
        GetDocumentReferencesAsync(sourceReference, cancellationToken);

    static TextChunk ToTextChunkMetadata(VectorChunk c) =>
        new()
        {
            ID = c.Id,
            Content = c.Content,
            DocumentReference = c.DocumentReference,
            SourceReference = c.SourceReference,
            Order = c.Order
        };
}
