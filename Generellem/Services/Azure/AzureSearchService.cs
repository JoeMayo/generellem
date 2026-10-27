using System.Net;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Services.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace Generellem.Services.Azure;

public class AzureSearchService(
    IDynamicConfiguration config,
    ILogger<AzureSearchService> logger,
    IOptions<GenerellemOptions>? options = null) : ISearchService
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

    const string VectorAlgorithmConfigName = "hnsw-config";
    const string VectorProfileName = "generellem-vector-profile";

    string? SearchServiceAdminApiKey => config[GKeys.AzSearchServiceAdminApiKey];
    string? SearchServiceEndpoint => config[GKeys.AzSearchServiceEndpoint];
    string? SearchServiceIndex => config[GKeys.AzSearchServiceIndex];

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
        if (await DoesIndexExistAsync(cancelToken))
        {
            await EnsureFilterFieldsAsync(cancelToken);
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchIndex searchIndex = new(SearchServiceIndex)
        {
            VectorSearch = new()
            {
                Profiles =
                {
                    new VectorSearchProfile(VectorProfileName, VectorAlgorithmConfigName)
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(VectorAlgorithmConfigName)
                }
            },
        };

        foreach (SearchField field in AzureSearchSchema.CreateFields(vectorSearchDimensions, VectorProfileName))
            searchIndex.Fields.Add(field);

        SearchIndexClient indexClient = new(endpoint, credential);

        try
        {
            await pipeline.ExecuteAsync(
                async token => await indexClient.CreateOrUpdateIndexAsync(searchIndex, cancellationToken: token),
                cancelToken);
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }    
    }

    protected virtual async Task<bool> DoesIndexExistAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchIndexClient indexClient = new SearchIndexClient(endpoint, credential);

        try
        {
            SearchIndex index =
                await pipeline.ExecuteAsync(
                    async token => await indexClient.GetIndexAsync(SearchServiceIndex, cancellationToken),
                    cancellationToken);
            return index != null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // 404 indicates the index does not exist
            return false;
        }
    }

    public virtual async Task DeleteDocumentReferencesAsync(List<string> idsToDelete, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchClient searchClient = new(endpoint, SearchServiceIndex, credential);

        IndexDocumentsBatch<SearchDocument> batch = IndexDocumentsBatch.Delete(nameof(TextChunk.ID), idsToDelete);
        await searchClient.IndexDocumentsAsync(batch, null, cancellationToken);
    }

    public async Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return new();

        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));
        
        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchClient searchClient = new(endpoint, SearchServiceIndex, credential);

        SearchOptions options = new()
        {
            Filter = $"search.ismatch('{documentReference}*', '{nameof(TextChunk.DocumentReference)}')"
        };
        options.Select.Add(nameof(TextChunk.ID));
        options.Select.Add(nameof(TextChunk.DocumentReference));

        List<TextChunk> chunks = new();

        try
        {
            Response<SearchResults<TextChunk>> response =
                await pipeline.ExecuteAsync(
                    async token => await searchClient.SearchAsync<TextChunk>(string.Empty, options, cancellationToken),
                    cancellationToken);

            await foreach (SearchResult<TextChunk> result in response.Value.GetResultsAsync())
                chunks.Add(result.Document);
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }

        return chunks;
    }

    public async Task<List<TextChunk>> GetDocumentReferencesAsync(string docSourcePrefix, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return new();

        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));
        
        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchClient searchClient = new(endpoint, SearchServiceIndex, credential);

        SearchOptions options = new()
        {
            Filter = $"search.ismatch('{docSourcePrefix}*', '{nameof(TextChunk.DocumentReference)}')"
        };
        options.Select.Add(nameof(TextChunk.ID));
        options.Select.Add(nameof(TextChunk.DocumentReference));

        List<TextChunk> chunks = new();

        try
        {
            Response<SearchResults<TextChunk>> response =
                await pipeline.ExecuteAsync(
                    async token => await searchClient.SearchAsync<TextChunk>(string.Empty, options, cancellationToken),
                    cancellationToken);

            await foreach (SearchResult<TextChunk> result in response.Value.GetResultsAsync())
                chunks.Add(result.Document);
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }

        return chunks;
    }

    public virtual async Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken)
    {
        if (await DoesIndexExistAsync(cancelToken))
            await EnsureFilterFieldsAsync(cancelToken);

        string tenantId = config[GKeys.TenantID] ?? "0";
        string groupId = config[GKeys.GroupID] ?? "0";
        string path = config[GKeys.Path] ?? "?";
        List<SearchDocument> documentsToIndex = documents
            .Select(chunk => AzureSearchSchema.ToDocument(chunk, tenantId, groupId, path))
            .ToList();

        try
        {
            await IndexDocumentsAsync(documentsToIndex, cancelToken);
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }    
    }

    public virtual async Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchClient searchClient = new(endpoint, SearchServiceIndex, credential);

        var searchOptions = new SearchOptions
        {
            VectorSearch = new()
            {
                Queries = { new VectorizedQuery(embedding) { KNearestNeighborsCount = VectorSearchLimit, Fields = { nameof(TextChunk.Embedding) } } }
            }
        };

        try
        {
            SearchResults<TextChunk> results = await pipeline.ExecuteAsync<SearchResults<TextChunk>>(
                async token => await searchClient.SearchAsync<TextChunk>(searchOptions, cancellationToken: token),
                cancelToken);
        
            List<TextChunk> chunks =
                (from chunk in results.GetResultsAsync().ToBlockingEnumerable(cancelToken)
                 select chunk.Document)
                .ToList();

            return chunks;
        }
        catch (RequestFailedException rfNf) when (rfNf.Status == (int)HttpStatusCode.NotFound)
        {
            throw new GenerellemNeedsIngestionException(
                "You need to perform ingestion before querying so that there are documents available for context.",  
                rfNf);
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }

    public Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken)
    {
        string filter = AzureSearchSchema.PathFilter(
            path,
            config[GKeys.TenantID] ?? "0",
            config[GKeys.GroupID] ?? "0");

        return QueryByFilterAsync(filter, cancellationToken);
    }

    public async Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return;

        await EnsureFilterFieldsAsync(cancellationToken);

        List<string> ids = await QueryIdsByFilterAsync(AzureSearchSchema.TenantFilter(companyId), cancellationToken);
        if (ids.Count == 0)
            return;

        await DeleteDocumentReferencesAsync(ids, cancellationToken);
    }

    public Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken)
    {
        string filter = AzureSearchSchema.SourceReferenceFilter(
            sourceReference,
            config[GKeys.TenantID] ?? "0",
            config[GKeys.GroupID] ?? "0");

        return QueryByFilterAsync(filter, cancellationToken);
    }

    protected virtual async Task EnsureFilterFieldsAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);
        SearchIndexClient indexClient = new(endpoint, credential);

        SearchIndex index = await pipeline.ExecuteAsync(
            async token => await indexClient.GetIndexAsync(SearchServiceIndex, token),
            cancellationToken);

        List<string> added = [];
        foreach (string name in AzureSearchSchema.FilterFields)
        {
            if (index.Fields.Any(field => field.Name == name))
                continue;

            index.Fields.Add(new SimpleField(name, SearchFieldDataType.String) { IsFilterable = true });
            added.Add(name);
        }

        if (added.Count == 0)
            return;

        await pipeline.ExecuteAsync(
            async token => await indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: token),
            cancellationToken);

        logger.LogInformation(GenerellemLogEvents.Information, "Added Azure AI Search filter fields: {Fields}", string.Join(", ", added));
    }

    protected virtual Task IndexDocumentsAsync(IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
    {
        SearchClient searchClient = CreateSearchClient();
        return IndexCoreAsync();

        async Task IndexCoreAsync()
        {
            await pipeline.ExecuteAsync(
                async token => await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(documents), cancellationToken: token),
                cancellationToken);
        }
    }

    protected virtual async Task<List<TextChunk>> QueryByFilterAsync(string filter, CancellationToken cancellationToken)
    {
        if (!await DoesIndexExistAsync(cancellationToken))
            return [];

        await EnsureFilterFieldsAsync(cancellationToken);

        SearchClient searchClient = CreateSearchClient();
        List<TextChunk> chunks = [];
        int skip = 0;
        const int pageSize = 1000;

        try
        {
            while (true)
            {
                SearchOptions options = new()
                {
                    Filter = filter,
                    Size = pageSize,
                    Skip = skip
                };
                options.Select.Add(nameof(TextChunk.ID));
                options.Select.Add(nameof(TextChunk.DocumentReference));
                options.Select.Add(nameof(TextChunk.SourceReference));

                Response<SearchResults<TextChunk>> response =
                    await pipeline.ExecuteAsync(
                        async token => await searchClient.SearchAsync<TextChunk>("*", options, token),
                        cancellationToken);

                int pageCount = 0;
                await foreach (SearchResult<TextChunk> result in response.Value.GetResultsAsync())
                {
                    chunks.Add(result.Document);
                    pageCount++;
                }

                if (pageCount < pageSize)
                    break;

                skip += pageCount;
                if (skip > 100_000)
                    throw new InvalidOperationException("Azure AI Search returned more matching documents than can be paged.");
            }
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }

        return chunks;
    }

    protected virtual async Task<List<string>> QueryIdsByFilterAsync(string filter, CancellationToken cancellationToken)
    {
        List<TextChunk> chunks = await QueryByFilterAsync(filter, cancellationToken);
        return chunks
            .Select(chunk => chunk.ID)
            .OfType<string>()
            .Where(id => id.Length > 0)
            .ToList();
    }

    SearchClient CreateSearchClient()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);
        return new SearchClient(endpoint, SearchServiceIndex, credential);
    }
}
