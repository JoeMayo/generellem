using System.Net;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Generellem.Embedding;
using Generellem.Services.Exceptions;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace Generellem.Services.Azure;

public class AzureSearchService(IDynamicConfiguration config, ILogger<AzureSearchService> logger) : ISearchService
{
    const int VectorSearchDimensions = 1536; // defined by text-embedding-ada-002
    const string VectorAlgorithmConfigName = "hnsw-config";
    const string VectorProfileName = "generellem-vector-profile";

    string? SearchServiceAdminApiKey => config[GKeys.AzSearchServiceAdminApiKey];
    string? SearchServiceEndpoint => config[GKeys.AzSearchServiceEndpoint];
    string? SearchServiceIndex => config[GKeys.AzSearchServiceIndex];

    readonly ResiliencePipeline pipeline = 
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions())
            .AddTimeout(TimeSpan.FromSeconds(3))
            .Build();

    public virtual async Task CreateIndexAsync(CancellationToken cancelToken)
    {
        if (await DoesIndexExistAsync(cancelToken))
            return;

        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchIndex searchIndex = new(SearchServiceIndex)
        {
            Fields =
            {
                new SimpleField(nameof(TextChunk.ID), SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchableField(nameof(TextChunk.DocumentReference)) { IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchableField(nameof(TextChunk.Content)) { IsFilterable = true },
                new VectorSearchField(nameof(TextChunk.Embedding), VectorSearchDimensions, VectorProfileName)
            },
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

    async Task<bool> DoesIndexExistAsync(CancellationToken cancellationToken)
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

    public async Task DeleteDocumentReferencesAsync(List<string> idsToDelete, CancellationToken cancellationToken)
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
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceAdminApiKey, nameof(SearchServiceAdminApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchServiceEndpoint, nameof(SearchServiceEndpoint));

        Uri endpoint = new(SearchServiceEndpoint);
        AzureKeyCredential credential = new(SearchServiceAdminApiKey);

        SearchClient searchClient = new(endpoint, SearchServiceIndex, credential);

        try
        {
            await pipeline.ExecuteAsync(
                async token => await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(documents), cancellationToken: token),
                cancelToken);
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
                Queries = { new VectorizedQuery(embedding) { KNearestNeighborsCount = 3, Fields = { nameof(TextChunk.Embedding) } } }
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
        throw new NotImplementedException();
    }

    public Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
