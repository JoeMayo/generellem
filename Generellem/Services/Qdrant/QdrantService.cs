using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Services.Exceptions;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Generellem.Services.Qdrant;

public class QdrantService(
    IDynamicConfiguration config,
    ILogger<QdrantService> logger,
    IOptions<GenerellemOptions>? options = null,
    QdrantClient? qdrantClient = null) : ISearchService
{
    readonly int vectorSearchDimensions = options?.Value.EmbeddingDimensions > 0
        ? options.Value.EmbeddingDimensions
        : 1536;

    /// <summary>
    /// Neighbors requested per query. Read from <see cref="GenerellemOptions.TopK"/>.
    /// </summary>
    public int VectorSearchLimit { get; } = options?.Value.TopK > 0
        ? options.Value.TopK
        : 3;

    const string DocumentReference = "DocumentReference";
    const string ID = "ID";
    const string Content = "Content";
    const string GroupID = "GroupID";
    const string Pathname = "Path";
    const string SourceReference = "SourceReference";
    const string TenantID = "TenantID";

    /// <summary>
    /// Payload fields indexed for filtering. Azure AI Search indexes the same names.
    /// </summary>
    public static IReadOnlyList<string> FilterPayloadFields { get; } =
    [
        TenantID,
        GroupID,
        SourceReference,
        Pathname,
        DocumentReference
    ];

    string? QdrantApiKey => config[GKeys.QdrantApiKey];
    string? QdrantEndpoint => config[GKeys.QdrantEndpoint]; //"http://localhost:6334";
    string? QdrantCollection => config[GKeys.QdrantCollection];

    readonly QdrantClient? sharedClient = qdrantClient;

    CollectionExistenceCache existenceCache = new(TimeProvider.System, CollectionExistenceCache.DefaultTtl);

    QdrantClient Client => sharedClient ?? throw new InvalidOperationException(
        "QdrantClient is not registered. AddGenerellem(GenerellemProfile.Azure) registers a shared client.");

    /// <summary>
    /// Replaces the existence cache. Tests use this to control the clock and TTL.
    /// </summary>
    protected void UseCollectionCache(TimeProvider time, TimeSpan ttl) =>
        existenceCache = new CollectionExistenceCache(time, ttl);

    /// <summary>
    /// Drops a cached "collection exists" answer so the next check asks Qdrant.
    /// </summary>
    protected void InvalidateCollectionCache() => existenceCache.Invalidate();

    readonly ResiliencePipeline pipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,  // Adds a random factor to the delay
                    MaxRetryAttempts = 10,
                    Delay = TimeSpan.FromSeconds(3),
                })
            .AddTimeout(TimeSpan.FromSeconds(3))
            .Build();

    public virtual async Task CreateIndexAsync(CancellationToken cancelToken)
    {
        // Ask Qdrant directly. A cached "exists" must not skip recreation after an external delete.
        if (await CollectionExistsOnServerAsync(cancelToken))
        {
            existenceCache.RememberExists();
            return;
        }

        existenceCache.Invalidate();

        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        try
        {
            // Resolve before the retry pipeline so a missing client fails immediately.
            QdrantClient client = Client;

            await pipeline.ExecuteAsync(
                async token =>
                    await client.CreateCollectionAsync(
                        collectionName: QdrantCollection,
                        vectorsConfig: new VectorParams { Size = (ulong)vectorSearchDimensions, Distance = Distance.Cosine },
                        hnswConfig: new HnswConfigDiff { PayloadM = 16, M = 0 }),
                cancelToken);

            foreach (string field in FilterPayloadFields)
            {
                PayloadIndexParams? indexParams = field == TenantID
                    ? new PayloadIndexParams
                    {
                        KeywordIndexParams = new KeywordIndexParams
                        {
                            IsTenant = true
                        }
                    }
                    : null;

                await client.CreatePayloadIndexAsync(
                    collectionName: QdrantCollection,
                    fieldName: field,
                    schemaType: PayloadSchemaType.Keyword,
                    indexParams: indexParams);
            }

            existenceCache.RememberExists();
        }
        catch (RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
                InvalidateCollectionCache();

            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rpcEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public async Task<bool> DoesIndexExistAsync(CancellationToken cancellationToken)
    {
        if (existenceCache.TryGetExists())
            return true;

        if (!await CollectionExistsOnServerAsync(cancellationToken))
            return false;

        existenceCache.RememberExists();
        return true;
    }

    /// <summary>
    /// Asks Qdrant whether the collection exists. Does not read the cache.
    /// NotFound and Unimplemented mean the collection is missing.
    /// </summary>
    protected virtual async Task<bool> CollectionExistsOnServerAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        try
        {
            return await pipeline.ExecuteAsync(
                async token => await Client.CollectionExistsAsync(QdrantCollection, token),
                cancellationToken);
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound || rpcEx.StatusCode == StatusCode.Unimplemented)
        {
            InvalidateCollectionCache();
            return false;
        }
    }

    public async Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        Filter filter = new()
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition { Key = TenantID, Match = new Match { Keyword = companyId.ToString() } },
                }
            }
        };

        try
        {
            UpdateResult result =
                await pipeline.ExecuteAsync(
                    async token => await Client.DeleteAsync(QdrantCollection, filter),
                    cancellationToken);
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound)
        {
            // 404 indicates the index does not exist. Drop the cache so the next check sees that.
            InvalidateCollectionCache();
        }
    }

    public async Task DeleteDocumentReferencesAsync(List<string> idsToDelete, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        IReadOnlyList<Guid> ids = idsToDelete.Select(id => Guid.Parse(id)).ToList();
        try
        {
            UpdateResult result =
                await pipeline.ExecuteAsync(
                    async token => await Client.DeleteAsync(QdrantCollection, ids),
                    cancellationToken);
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound)
        {
            // 404 indicates the index does not exist. Drop the cache so the next check sees that.
            InvalidateCollectionCache();
        }
    }

    public async Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return new();

        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        string QdrantTenantID = config[GKeys.TenantID] ?? "0";
        string QdrantGroupID = config[GKeys.GroupID] ?? "0";
        string QdrantPath = config[GKeys.Path] ?? "?";

        try
        {
            Filter filter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition { Key = DocumentReference, Match = new Match { Keyword = documentReference } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = TenantID, Match = new Match { Keyword = QdrantTenantID } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = GroupID, Match = new Match { Keyword = QdrantGroupID } },
                    },
                }
            };

            WithPayloadSelector payloadSelector =
                new()
                {
                    Include = new PayloadIncludeSelector
                    {
                        Fields = { new string[] { ID, DocumentReference } }
                    }
                };

            IReadOnlyList<ScoredPoint> queryResult =
                await pipeline.ExecuteAsync(
                    async token => await Client.QueryAsync(
                        collectionName: QdrantCollection,
                        filter: filter,
                        payloadSelector: true,
                        vectorsSelector: true),
                    cancellationToken);

            List<TextChunk> chunks =
                (from doc in queryResult
                 select new TextChunk
                 {
                     ID = doc.Id.Uuid,
                     DocumentReference = doc.Payload[DocumentReference].StringValue
                 })
                .ToList();

            return chunks;
        }
        catch (RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
                InvalidateCollectionCache();

            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rpcEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public virtual async Task<List<TextChunk>> GetDocumentReferencesAsync(string docSourcePrefix, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return new();

        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        string qdrantTenantID = config[GKeys.TenantID] ?? "0";
        string qdrantGroupID = config[GKeys.GroupID] ?? "0";

        try
        {
            Filter filter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition { Key = SourceReference, Match = new Match { Keyword = docSourcePrefix } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = TenantID, Match = new Match { Keyword = qdrantTenantID } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = GroupID, Match = new Match { Keyword = qdrantGroupID } },
                    },
                }
            };

            WithPayloadSelector payloadSelector =
                new()
                {
                    Include = new PayloadIncludeSelector
                    {
                        Fields = { new string[] { ID, DocumentReference } }
                    }
                };

            List<TextChunk> allChunks = new();

            const ulong limit = 1000;

            ulong resultCount = 0;
            ulong offset = 0;
            do
            {

                IReadOnlyList<ScoredPoint> queryResult =
                    await pipeline.ExecuteAsync(
                        async token => await Client.QueryAsync(
                            collectionName: QdrantCollection,
                            filter: filter,
                            limit: limit,
                            offset: offset,
                            payloadSelector: true,
                            vectorsSelector: true),
                        cancellationToken);

                resultCount = (ulong)queryResult.Count;

                if (resultCount == 0)
                    break;

                offset += resultCount;

                List<TextChunk> chunks =
                    (from doc in queryResult
                     select new TextChunk
                     {
                         ID = doc.Id.Uuid,
                         DocumentReference = doc.Payload[DocumentReference].StringValue
                     })
                    .ToList();

                allChunks.AddRange(chunks);

            } while (resultCount > 0);

            return allChunks;
        }
        catch (RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
                InvalidateCollectionCache();

            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rpcEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public async Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return new();

        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        string qdrantTenantID = config[GKeys.TenantID] ?? "0";
        string qdrantGroupID = config[GKeys.GroupID] ?? "0";

        try
        {
            Filter filter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition { Key = Pathname, Match = new Match { Keyword = path } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = TenantID, Match = new Match { Keyword = qdrantTenantID } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = GroupID, Match = new Match { Keyword = qdrantGroupID } },
                    },
                }
            };

            WithPayloadSelector payloadSelector =
                new()
                {
                    Include = new PayloadIncludeSelector
                    {
                        Fields = { new string[] { ID, DocumentReference } }
                    }
                };

            IReadOnlyList<ScoredPoint> queryResult =
                await pipeline.ExecuteAsync(
                    async token => await Client.QueryAsync(
                        collectionName: QdrantCollection,
                        filter: filter,
                        payloadSelector: true,
                        vectorsSelector: true),
                    cancellationToken);

            List<TextChunk> chunks =
                (from doc in queryResult
                 select new TextChunk
                 {
                     ID = doc.Id.Uuid,
                     DocumentReference = doc.Payload[DocumentReference].StringValue
                 })
                .ToList();

            return chunks;
        }
        catch (RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
                InvalidateCollectionCache();

            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rpcEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public virtual async Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        string qdrantTenantID = config[GKeys.TenantID] ?? "0";
        string qdrantGroupID = config[GKeys.GroupID] ?? "0";
        string qdrantPath = config[GKeys.Path] ?? "?";

        try
        {
            IReadOnlyList<PointStruct> points =
                (from doc in documents
                 select new PointStruct
                 {
                     Id = new PointId() { Uuid = doc.ID! },
                     Payload =
                     {
                        [DocumentReference] = doc.DocumentReference!,
                        [SourceReference] = doc.SourceReference!,
                        [Content] = doc.Content!,
                        [Pathname] = qdrantPath,
                        [TenantID] = qdrantTenantID,
                        [GroupID] = qdrantGroupID
                     },
                     Vectors = doc.Embedding.ToArray()
                 })
                .ToList();

            await pipeline.ExecuteAsync(
                async token => await Client.UpsertAsync(QdrantCollection, points),
                cancelToken);
        }
        catch (RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
                InvalidateCollectionCache();

            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rpcEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public virtual async Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantApiKey, nameof(QdrantApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantEndpoint, nameof(QdrantEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(QdrantCollection, nameof(QdrantCollection));

        string qdrantTenantID = config[GKeys.TenantID] ?? "0";
        string qdrantGroupID = config[GKeys.GroupID] ?? "0";

        try
        {
            Filter filter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition { Key = TenantID, Match = new Match { Keyword = qdrantTenantID } },
                    },
                    new Condition
                    {
                        Field = new FieldCondition { Key = GroupID, Match = new Match { Keyword = qdrantGroupID } },
                    },
                }
            };

            IReadOnlyList<ScoredPoint> queryResult = await pipeline.ExecuteAsync(
                async token =>
                {
                    return await Client.SearchAsync(
                        collectionName: QdrantCollection,
                        filter: filter,
                        vector: embedding.ToArray(),
                        limit: (ulong)VectorSearchLimit);
                },
                cancelToken);

            List<TextChunk> chunks =
                (from doc in queryResult
                 select new TextChunk
                 {
                     ID = doc.Id.Uuid,
                     Content = doc.Payload[Content].StringValue,
                     DocumentReference = doc.Payload[DocumentReference].StringValue,
                 })
                .ToList();

            return chunks;
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound)
        {
            InvalidateCollectionCache();
            throw new GenerellemNeedsIngestionException(
                "You need to perform ingestion before querying so that there are documents available for context.",
                rpcEx);
        }
    }

    public virtual Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken) =>
        GetDocumentReferencesAsync(sourceReference, cancellationToken);
}
