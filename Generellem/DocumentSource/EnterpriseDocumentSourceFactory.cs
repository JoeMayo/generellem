using Generellem.Services;

using Microsoft.Extensions.Logging;

namespace Generellem.DocumentSource;

/// <summary>
/// These are the document sources that are on an internal file server or website.
/// </summary>
/// <param name="httpFact">Get a reference to an HttpClient instance.</param>
/// <param name="logger">For writing to a log file.</param>
/// <param name="pathProviderFactory">Generates a new <see cref="IPathProvider"/>.</param>
public class EnterpriseDocumentSourceFactory(
    IGenerellemHttpClientFactory httpFact, 
    ILogger<Website> logger,
    IPathProviderFactory pathProviderFactory) 
    : IDocumentSourceFactory
{
    public IEnumerable<IDocumentSource> GetDocumentSources() => 
        [
            new FileSystem(pathProviderFactory),
            new Website(httpFact, logger, pathProviderFactory)
        ];
}
