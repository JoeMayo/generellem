using Generellem.Document;
using Generellem.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Generellem.DependencyInjection;

/// <summary>
/// Checks profile options and required configuration keys before other hosted services start.
/// </summary>
/// <remarks>
/// InMemory and Sqlite require no configuration keys.
/// Ollama uses its option defaults and only fails when those values are blank.
/// Azure requires the Azure OpenAI keys plus either Qdrant or Azure AI Search.
/// Blob, Cosmos, and Microsoft Graph keys belong to optional hosts and are not required here.
/// An empty <see cref="GenerellemOptions.McpServers"/> list is valid on every profile.
/// </remarks>
public class GenerellemStartupValidator(
    IOptions<GenerellemOptions> options,
    IDynamicConfiguration configuration,
    IImageTextExtractor? images = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (images is not null)
            ImageText.Current = images;

        GenerellemOptions opts = options.Value;
        List<string> errors = [];

        if (opts.ChunkSize <= 0)
            errors.Add($"{nameof(GenerellemOptions.ChunkSize)} must be greater than 0.");

        if (opts.ChunkOverlap < 0 || opts.ChunkOverlap >= opts.ChunkSize)
            errors.Add($"{nameof(GenerellemOptions.ChunkOverlap)} must be zero or greater and less than {nameof(GenerellemOptions.ChunkSize)}.");

        if (opts.TopK <= 0)
            errors.Add($"{nameof(GenerellemOptions.TopK)} must be greater than 0.");

        if (opts.EmbeddingDimensions <= 0)
            errors.Add($"{nameof(GenerellemOptions.EmbeddingDimensions)} must be greater than 0.");

        if (opts.EmbeddingBatchSize <= 0)
            errors.Add($"{nameof(GenerellemOptions.EmbeddingBatchSize)} must be greater than 0.");

        if (opts.EnableImageVision && string.IsNullOrWhiteSpace(opts.VisionModel))
            errors.Add($"{nameof(GenerellemOptions.VisionModel)} is required when {nameof(GenerellemOptions.EnableImageVision)} is true.");

        ValidateOpenAiCompatibleEndpoint(
            errors,
            opts.ChatEndpoint,
            opts.ChatModel,
            opts.ChatApiKey,
            nameof(GenerellemOptions.ChatEndpoint),
            nameof(GenerellemOptions.ChatModel),
            nameof(GenerellemOptions.ChatApiKey));

        ValidateOpenAiCompatibleEndpoint(
            errors,
            opts.EmbeddingEndpoint,
            opts.EmbeddingModel,
            opts.EmbeddingApiKey,
            nameof(GenerellemOptions.EmbeddingEndpoint),
            nameof(GenerellemOptions.EmbeddingModel),
            nameof(GenerellemOptions.EmbeddingApiKey));

        ValidateMcpServers(errors, opts);

        if (opts.Profile == GenerellemProfile.Ollama)
        {
            if (string.IsNullOrWhiteSpace(opts.OllamaEndpoint))
                errors.Add($"{nameof(GenerellemOptions.OllamaEndpoint)} is required for the Ollama profile.");

            if (string.IsNullOrWhiteSpace(opts.OllamaChatModel))
                errors.Add($"{nameof(GenerellemOptions.OllamaChatModel)} is required for the Ollama profile.");

            if (string.IsNullOrWhiteSpace(opts.OllamaEmbeddingModel))
                errors.Add($"{nameof(GenerellemOptions.OllamaEmbeddingModel)} is required for the Ollama profile.");
        }

        foreach (string key in GetRequiredConfigurationKeys(opts))
        {
            if (string.IsNullOrWhiteSpace(configuration[key]))
                errors.Add($"Missing required configuration key '{key}' for the {opts.Profile} profile.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Generellem startup configuration is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Configuration keys that must be present before the selected profile can run.
    /// </summary>
    public static IReadOnlyList<string> GetRequiredConfigurationKeys(GenerellemOptions options) =>
        options.Profile switch
        {
            GenerellemProfile.Azure => AzureKeys(options),
            _ => []
        };

    static void ValidateOpenAiCompatibleEndpoint(
        List<string> errors,
        string endpoint,
        string model,
        string apiKey,
        string endpointName,
        string modelName,
        string apiKeyName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        if (!IsAbsoluteHttpUri(endpoint))
            errors.Add($"{endpointName} must be an absolute http or https URI.");
        else if (string.IsNullOrWhiteSpace(apiKey) && !IsLoopback(endpoint))
            errors.Add($"{apiKeyName} is required when {endpointName} is not a loopback address.");

        if (string.IsNullOrWhiteSpace(model))
            errors.Add($"{modelName} is required when {endpointName} is set.");
    }

    static void ValidateMcpServers(List<string> errors, GenerellemOptions opts)
    {
        if (opts.McpMaxBlobBytes <= 0)
            errors.Add($"{nameof(GenerellemOptions.McpMaxBlobBytes)} must be greater than 0.");

        IReadOnlyList<McpServerOptions> servers = opts.McpServers ?? [];
        for (int i = 0; i < servers.Count; i++)
        {
            McpServerOptions? server = servers[i];
            string label = $"{nameof(GenerellemOptions.McpServers)}[{i}]";
            if (server is null)
            {
                errors.Add($"{label} is required.");
                continue;
            }

            if (!McpServerOptions.IsValidName(server.Name))
                errors.Add($"{label}.{nameof(McpServerOptions.Name)} must match {McpServerOptions.NamePatternText}.");

            if (server.MaxResources <= 0)
                errors.Add($"{label}.{nameof(McpServerOptions.MaxResources)} must be greater than 0.");

            bool hasCommand = !string.IsNullOrWhiteSpace(server.Command);
            bool hasUrl = !string.IsNullOrWhiteSpace(server.Url);
            if (hasCommand && hasUrl)
                errors.Add($"{label} must not set both {nameof(McpServerOptions.Command)} and {nameof(McpServerOptions.Url)}.");

            switch (server.Transport)
            {
                case McpTransport.Stdio:
                    if (!hasCommand)
                        errors.Add($"{label}.{nameof(McpServerOptions.Command)} is required for stdio.");
                    if (hasUrl)
                        errors.Add($"{label}.{nameof(McpServerOptions.Url)} is not allowed for stdio.");
                    if (server.Headers is { Count: > 0 })
                        errors.Add($"{label}.{nameof(McpServerOptions.Headers)} are not allowed for stdio.");
                    break;

                case McpTransport.Http:
                    if (!hasUrl)
                        errors.Add($"{label}.{nameof(McpServerOptions.Url)} is required for http.");
                    else if (!IsAbsoluteHttpUri(server.Url))
                        errors.Add($"{label}.{nameof(McpServerOptions.Url)} must be an absolute http or https URI.");
                    if (hasCommand)
                        errors.Add($"{label}.{nameof(McpServerOptions.Command)} is not allowed for http.");
                    if (server.Arguments is { Count: > 0 })
                        errors.Add($"{label}.{nameof(McpServerOptions.Arguments)} are not allowed for http.");
                    if (server.Environment is { Count: > 0 })
                        errors.Add($"{label}.{nameof(McpServerOptions.Environment)} is not allowed for http.");
                    break;

                default:
                    errors.Add($"{label}.{nameof(McpServerOptions.Transport)} must be stdio or http.");
                    break;
            }
        }

        AddMcpNameCollisions(errors, servers);
    }

    static void AddMcpNameCollisions(List<string> errors, IReadOnlyList<McpServerOptions> servers)
    {
        List<string> names = [];
        foreach (McpServerOptions? server in servers)
        {
            if (server is not null && McpServerOptions.IsValidName(server.Name))
                names.Add(server.Name);
        }

        for (int i = 0; i < names.Count; i++)
        {
            for (int j = i + 1; j < names.Count; j++)
            {
                string left = names[i];
                string right = names[j];
                if (string.Equals(left, right, StringComparison.Ordinal))
                    errors.Add($"{nameof(GenerellemOptions.McpServers)} names must be unique. '{left}' is repeated.");
                else if (right.StartsWith(left, StringComparison.Ordinal))
                    errors.Add($"{nameof(GenerellemOptions.McpServers)} names must not be a prefix of each other. '{left}' is a prefix of '{right}'.");
                else if (left.StartsWith(right, StringComparison.Ordinal))
                    errors.Add($"{nameof(GenerellemOptions.McpServers)} names must not be a prefix of each other. '{right}' is a prefix of '{left}'.");
            }
        }
    }

    static bool IsAbsoluteHttpUri(string endpoint) =>
        Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri) &&
        uri.Scheme is "http" or "https";

    static bool IsLoopback(string endpoint) =>
        Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri) && uri.IsLoopback;

    static IReadOnlyList<string> AzureKeys(GenerellemOptions options)
    {
        List<string> keys =
        [
            GKeys.AzOpenAIApiKey,
            GKeys.AzOpenAIDeploymentName,
            GKeys.AzOpenAIEmbeddingName,
            GKeys.AzOpenAIEndpointName
        ];

        if (options.UseAzureSearch)
        {
            keys.Add(GKeys.AzSearchServiceAdminApiKey);
            keys.Add(GKeys.AzSearchServiceEndpoint);
            keys.Add(GKeys.AzSearchServiceIndex);
        }
        else
        {
            keys.Add(GKeys.QdrantApiKey);
            keys.Add(GKeys.QdrantCollection);
            keys.Add(GKeys.QdrantEndpoint);
        }

        return keys;
    }
}