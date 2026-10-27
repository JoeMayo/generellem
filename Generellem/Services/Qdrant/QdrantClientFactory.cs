using Qdrant.Client;

namespace Generellem.Services.Qdrant;

/// <summary>
/// Builds the process-wide <see cref="QdrantClient"/> from Generellem configuration.
/// </summary>
public static class QdrantClientFactory
{
    public static QdrantClient Create(string? endpoint, string? apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        Uri address = new(endpoint);
        bool https = endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        return new QdrantClient(address.Host, apiKey: apiKey, https: https);
    }
}
