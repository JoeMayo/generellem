using Qdrant.Client;

namespace Generellem.Services.Qdrant;

/// <summary>
/// Builds the process-wide <see cref="QdrantClient"/> from Generellem configuration.
/// </summary>
public static class QdrantClientFactory
{
    /// <summary>
    /// Qdrant gRPC port used when the endpoint URL does not name a port.
    /// Cloud URLs such as <c>https://example.cloud.qdrant.io</c> omit the port and still use 6334.
    /// </summary>
    public const int DefaultGrpcPort = 6334;

    public static QdrantClient Create(string? endpoint, string? apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        Uri address = new(endpoint);
        bool https = endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        return new QdrantClient(
            address.Host,
            port: ResolveGrpcPort(address),
            apiKey: apiKey,
            https: https);
    }

    /// <summary>
    /// Uses the URL port when one is present so a mapped Testcontainers port reaches gRPC.
    /// A URL without a port keeps <see cref="DefaultGrpcPort"/> instead of HTTP 80 or HTTPS 443.
    /// </summary>
    public static int ResolveGrpcPort(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsDefaultPort ? DefaultGrpcPort : address.Port;
    }
}
