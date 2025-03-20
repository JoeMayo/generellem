using Generellem.Document.DocumentTypes;
using Generellem.Services;

using HtmlAgilityPack;

using Microsoft.Extensions.Logging;

using System.Runtime.CompilerServices;
using System.Text;

using IHttpClientFactory = Generellem.Services.IHttpClientFactory;

namespace Generellem.DocumentSource;

/// <summary>
/// Manages ingestion of a website
/// </summary>
public class Website : IDocumentSource
{
    /// <summary>
    /// Describes the document source.
    /// </summary>
    public string Description { get; set; } = "Web Site";

    public string Reference { get; set; } = $"{Environment.MachineName}:{nameof(Website)}";

    readonly HttpClient httpClient;
    readonly ILogger<Website> logger;
    readonly IPathProvider pathProvider;

    public Website(IHttpClientFactory httpFact, ILogger<Website> logger, IPathProviderFactory pathProviderFact)
    {
        this.httpClient = httpFact.Create();
        this.logger = logger;
        this.pathProvider = pathProviderFact.Create(this);
    }

    /// <summary>
    /// Iteratively visits the page of each website for caller processing
    /// </summary>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>foreachable sequence of <see cref="DocumentInfo"/></returns>
    public async IAsyncEnumerable<DocumentInfo> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancelToken)
    {
        IEnumerable<PathSpec> websites = await pathProvider.GetPathsAsync($"{nameof(Website)}.json");

        foreach (PathSpec spec in websites)
        {
            if (spec?.Path is null) continue;

            string specDescription = spec.Description ?? string.Empty;

            await foreach (DocumentInfo page in GetPagesAsync(spec.Path, specDescription, cancelToken))
                yield return page;
        }
    }

    async IAsyncEnumerable<DocumentInfo> GetPagesAsync(string url, string specDescription, [EnumeratorCancellation] CancellationToken cancelToken)
    {
        HashSet<string> alreadyVisited = new();

        Html html = new();

        Queue<string> queue = new();
        queue.Enqueue(url);

        while (queue.Count is not 0 && !cancelToken.IsCancellationRequested)
        {
            string currentUrl = queue.Dequeue();

            if (alreadyVisited.Contains(currentUrl))
                continue;

            alreadyVisited.Add(currentUrl);

            string? htmlDocument = null;

            try
            {
                htmlDocument = await GetHtmlDocumentAsync(currentUrl, cancelToken);
            }
            catch (HttpRequestException httpReqEx)
            {
                logger.LogWarning(GenerellemLogEvents.HttpError, httpReqEx, "Unable to process URL: {CurrentURL}", currentUrl);
            }

            if (string.IsNullOrEmpty(htmlDocument))
                continue;

            MemoryStream memStream = new(Encoding.UTF8.GetBytes(htmlDocument))
            {
                Position = 0
            };

            yield return new DocumentInfo(Reference, memStream, html, currentUrl, specDescription);

            // Turning off recursion because we have reports of circularities where it doesn't stop ingesting.
            //var links = GetLinks(htmlDocument, url);

            //foreach (var link in links)
            //    queue.Enqueue(link);
        }
    }

    async Task<string> GetHtmlDocumentAsync(string url, CancellationToken cancelToken)
    {
        var response = await httpClient.GetAsync(url, cancelToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancelToken);
    }

    static List<string> GetLinks(string htmlDocument, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlDocument);

        HtmlNodeCollection? nodes = doc.DocumentNode.SelectNodes("//a[@href]");

        if (nodes == null)
            return new List<string>();

        List<string> links =
            (from node in nodes
             let href = node.GetAttributeValue("href", string.Empty)
             where href != null && href.StartsWith(baseUrl)
             select href)
            .ToList();

        return links;
    }
}