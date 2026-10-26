using System.Net;

using Generellem.Services;

using Microsoft.Extensions.Logging;

using Moq.Protected;

namespace Generellem.DocumentSource.Tests;

public class WebsiteTests
{
    readonly Mock<HttpClient> httpClientMock = new();
    readonly Mock<HttpMessageHandler> httpMsgHandlerMock = new();

    readonly Mock<IGenerellemHttpClientFactory> httpClientFactMock = new();
    readonly Mock<ILogger<Website>> loggerMock = new();
    readonly Mock<IPathProvider> pathProviderMock = new();
    readonly Mock<IPathProviderFactory> pathProviderFactMock = new();

    public WebsiteTests()
    {
        IEnumerable<PathSpec> pathSpecs =
            new List<PathSpec> 
            { 
                new PathSpec { Description = "Test", Path = "http://localhost" } 
            
            };
        httpClientMock = new Mock<HttpClient>(httpMsgHandlerMock.Object);

        httpClientFactMock
            .Setup(fact => fact.Create())
            .Returns(httpClientMock.Object);
        pathProviderMock
            .Setup(prov => prov.GetPathsAsync(It.IsAny<string>()))
            .ReturnsAsync(pathSpecs);
        pathProviderFactMock
            .Setup(fact => fact.Create(It.IsAny<IDocumentSource>()))
            .Returns(pathProviderMock.Object);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithValidUrl_ReturnsPages()
    {
        const string html = "<html><body><a href='page1'>Page 1</a><a href='page2'>Page 2</a></body></html>";

        Website website = new(httpClientFactMock.Object, loggerMock.Object, pathProviderFactMock.Object);
        httpMsgHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                nameof(HttpClient.SendAsync), 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(html) });
        List<DocumentInfo> docInfos = [];

        await foreach (DocumentInfo doc in website.GetDocumentsAsync(CancellationToken.None))
            docInfos.Add(doc);
            
        Assert.NotEmpty(docInfos);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithInvalidUrl_ReturnsEmpty()
    {
        Website website = new(httpClientFactMock.Object, loggerMock.Object, pathProviderFactMock.Object);
        httpMsgHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                nameof(HttpClient.SendAsync),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });
        List<DocumentInfo> docInfos = [];

        await foreach (DocumentInfo doc in website.GetDocumentsAsync(CancellationToken.None))
            docInfos.Add(doc);

        Assert.Empty(docInfos);
    }
}
