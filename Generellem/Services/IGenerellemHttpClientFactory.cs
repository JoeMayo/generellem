namespace Generellem.Services;

/// <summary>
/// Creates <see cref="HttpClient"/> instances for Generellem document and model calls.
/// Named to avoid colliding with <see cref="System.Net.Http.IHttpClientFactory"/>.
/// </summary>
public interface IGenerellemHttpClientFactory
{
    HttpClient Create();
}