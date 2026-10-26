using Generellem.Services;

using Microsoft.Extensions.Logging;

namespace Generellem.DocumentSource.Tests;

public class DemoDocumentSourceFactoryTests
{
    [Fact]
    public void GetDocumentSources_ReturnsExpected()
    {
        Mock<IGenerellemHttpClientFactory> httpClientFactMock = new();
        Mock<ILogger<Website>> loggerMock = new();
        Mock<IPathProviderFactory> pathProviderFactMock = new();

        EnterpriseDocumentSourceFactory testClass = new(httpClientFactMock.Object, loggerMock.Object, pathProviderFactMock.Object);

        IEnumerable<IDocumentSource> result = testClass.GetDocumentSources();

        Assert.Equal(2, result.Count());
        Assert.IsType<FileSystem>(result.ElementAt(0));
        Assert.IsType<Website>(result.ElementAt(1));
    }
}
