namespace Generellem.Services;

public class GenerellemHttpClientFactory : IGenerellemHttpClientFactory
{
    public HttpClient Create() => new HttpClient();
}