using System.Data;

using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Repository;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Generellem.Services.Sqlite;

/// <summary>
/// SQLite search. Keyword match uses FTS5. Vector match uses sqlite-vec when the
/// bundled extension loads, and otherwise a cosine scan of stored embeddings.
/// </summary>
public class SqliteSearchService(GenerellemContext ctx, IOptions<GenerellemOptions> options) : ISearchService
{
    readonly GenerellemOptions options = options.Value;
    readonly SqliteVec vec = new();

    public bool UsesVectorIndex => vec.IsEnabled;

    public string? VectorIndexDetail => vec.Detail;

    public async Task CreateIndexAsync(CancellationToken cancelToken) =>
        await WithConnectionAsync(cancelToken, connection =>
        {
            EnsureSchema(connection);
            return Task.CompletedTask;
        });

    public async Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        await ctx.VectorChunks.ExecuteDeleteAsync(cancellationToken);
        await WithConnectionAsync(cancellationToken, connection =>
        {
            EnsureSchema(connection);
            Execute(connection, "DELETE FROM VectorChunkFts");
            vec.DeleteAll(connection);
            return Task.CompletedTask;
        });
    }

    public async Task DeleteDocumentReferencesAsync(List<string> documentReferencesToDelete, CancellationToken cancellationToken)
    {
        if (documentReferencesToDelete.Count == 0)
            return;

        await ctx.VectorChunks
            .Where(c => documentReferencesToDelete.Contains(c.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await WithConnectionAsync(cancellationToken, connection =>
        {
            EnsureSchema(connection);
            foreach (string id in documentReferencesToDelete)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM VectorChunkFts WHERE Id = $id";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }

            vec.Delete(connection, documentReferencesToDelete);
            return Task.CompletedTask;
        });
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

        await WithConnectionAsync(cancelToken, connection =>
        {
            EnsureSchema(connection);
            foreach (TextChunk doc in documents)
            {
                using SqliteCommand delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM VectorChunkFts WHERE Id = $id";
                delete.Parameters.AddWithValue("$id", doc.ID!);
                delete.ExecuteNonQuery();

                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO VectorChunkFts(Id, Content) VALUES ($id, $content)";
                insert.Parameters.AddWithValue("$id", doc.ID!);
                insert.Parameters.AddWithValue("$content", doc.Content ?? string.Empty);
                insert.ExecuteNonQuery();

                if (vec.IsEnabled && doc.ID is not null)
                    vec.Upsert(connection, doc.ID, doc.Embedding);
            }

            return Task.CompletedTask;
        });
    }

    public Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken) =>
        SearchAsync(string.Empty, embedding, cancelToken);

    public async Task<List<TextChunk>> SearchAsync(string queryText, ReadOnlyMemory<float> embedding, CancellationToken cancelToken)
    {
        int topK = Math.Max(1, options.TopK);
        int candidates = Math.Max(topK, topK * 4);
        List<VectorChunk> rows = await ctx.VectorChunks.AsNoTracking().ToListAsync(cancelToken);
        List<TextChunk> vectorHits = await RankVectorsAsync(rows, embedding, candidates, cancelToken);

        if (string.IsNullOrWhiteSpace(queryText))
            return vectorHits.Take(topK).ToList();

        List<TextChunk> keywordHits = await KeywordHitsAsync(rows, queryText, candidates, cancelToken);
        return HybridRank.Fuse(topK, vectorHits, keywordHits);
    }

    public Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken) =>
        GetDocumentReferencesAsync(sourceReference, cancellationToken);

    async Task<List<TextChunk>> RankVectorsAsync(List<VectorChunk> rows, ReadOnlyMemory<float> embedding, int limit, CancellationToken cancelToken)
    {
        if (!vec.IsEnabled)
            await WithConnectionAsync(cancelToken, connection =>
            {
                EnsureSchema(connection);
                return Task.CompletedTask;
            });

        if (vec.IsEnabled)
        {
            List<string> ids = [];
            await WithConnectionAsync(cancelToken, connection =>
            {
                EnsureSchema(connection);
                ids = vec.Search(connection, embedding, limit);
                return Task.CompletedTask;
            });

            if (ids.Count > 0)
            {
                Dictionary<string, VectorChunk> byId = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
                List<TextChunk> indexed = [];
                foreach (string id in ids)
                {
                    if (byId.TryGetValue(id, out VectorChunk? row))
                        indexed.Add(ToTextChunkMetadata(row));
                }

                if (indexed.Count > 0)
                    return indexed;
            }
        }

        return rows
            .Select(row => (Row: row, Score: VectorMath.CosineSimilarity(embedding.Span, VectorMath.FromBytes(row.Embedding).Span)))
            .OrderByDescending(item => item.Score)
            .Take(limit)
            .Select(item => ToTextChunkMetadata(item.Row))
            .ToList();
    }

    async Task<List<TextChunk>> KeywordHitsAsync(
        List<VectorChunk> rows, string queryText, int limit, CancellationToken cancelToken)
    {
        string match = LexicalText.ToFtsMatch(queryText);
        if (match.Length == 0)
            return [];

        List<string> ids = [];
        await WithConnectionAsync(cancelToken, connection =>
        {
            EnsureSchema(connection);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id
                FROM VectorChunkFts
                WHERE VectorChunkFts MATCH $match
                ORDER BY rank
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$match", match);
            command.Parameters.AddWithValue("$limit", limit);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    ids.Add(reader.GetString(0));
            }

            return Task.CompletedTask;
        });

        Dictionary<string, VectorChunk> byId = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        List<TextChunk> hits = [];
        foreach (string id in ids)
        {
            if (byId.TryGetValue(id, out VectorChunk? row))
                hits.Add(ToTextChunkMetadata(row));
        }

        return hits;
    }

    void EnsureSchema(SqliteConnection connection)
    {
        Execute(
            connection,
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS VectorChunkFts USING fts5(
                Id UNINDEXED,
                Content,
                tokenize = 'unicode61'
            );
            """);
        vec.Ensure(connection, Math.Max(1, options.EmbeddingDimensions));
    }

    async Task WithConnectionAsync(CancellationToken cancelToken, Func<SqliteConnection, Task> action)
    {
        System.Data.Common.DbConnection connection = ctx.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancelToken);

        if (connection is not SqliteConnection sqlite)
            throw new InvalidOperationException("SQLite search requires a SqliteConnection.");

        await action(sqlite);
    }

    static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

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
