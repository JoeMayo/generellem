namespace Generellem.Services.Tests;

public class GenerellemHttpClientFactoryTests
{
    readonly GenerellemHttpClientFactory httpClientFact;

    public GenerellemHttpClientFactoryTests()
    {
        httpClientFact = new GenerellemHttpClientFactory();
    }

    [Fact]
    public void Create_ReturnsHttpClient()
    {
        HttpClient result = httpClientFact.Create();

        Assert.IsType<HttpClient>(result);
    }

    [Fact]
    public void Create_ReturnsNewInstance()
    {
        HttpClient client1 = httpClientFact.Create();
        HttpClient client2 = httpClientFact.Create();

        Assert.NotSame(client1, client2);
    }
}
