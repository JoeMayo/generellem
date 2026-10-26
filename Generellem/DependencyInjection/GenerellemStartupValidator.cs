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
/// </remarks>
public class GenerellemStartupValidator(
    IOptions<GenerellemOptions> options,
    IDynamicConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
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