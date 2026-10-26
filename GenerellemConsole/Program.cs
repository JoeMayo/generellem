using Generellem.DependencyInjection;
using Generellem.DocumentSource;
using Generellem.Repository;
using Generellem.Services;

using GenerellemConsole;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

CancellationTokenSource tokenSource = new();

GenerellemProfile profile = ParseProfile(args);

IHost host = InitializeConfiguration(args, profile);

// config file location for this demo only
GenerellemFiles.SubFolder = "ConsoleDemo";

ConfigureDB();

await host.RunAsync(tokenSource.Token);


IHost InitializeConfiguration(string[] args, GenerellemProfile selectedProfile)
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.Sources.Clear();

    IHostEnvironment env = builder.Environment;

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    ConfigureServices(builder.Services, builder.Configuration, selectedProfile);

#if DEBUG
    // User secrets only needed for Azure (and optionally Ollama overrides).
    if (selectedProfile == GenerellemProfile.Azure)
        builder.Configuration.AddUserSecrets<Program>();
#endif

    return builder.Build();
}

void ConfigureDB()
{
    using GenerellemContext ctx = new(host.Services.GetRequiredService<IGenerellemFiles>());

    try
    {
        ctx.Database.OpenConnection();
        ctx.Database.Migrate();
    }
    catch (Exception)
    {
        Console.WriteLine("Migration is up-to-date.");
    }
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration, GenerellemProfile selectedProfile)
{
    services.AddHostedService<GenerellemHostedService>();
    services.AddTransient<IDocumentSourceFactory, EnterpriseDocumentSourceFactory>();

    services.AddGenerellem(selectedProfile, options =>
    {
        IConfigurationSection section = configuration.GetSection(GenerellemOptions.SectionName);
        if (section.Exists())
        {
            string? dims = section["EmbeddingDimensions"];
            if (int.TryParse(dims, out int embeddingDimensions) && embeddingDimensions > 0)
                options.EmbeddingDimensions = embeddingDimensions;

            string? topK = section["TopK"];
            if (int.TryParse(topK, out int topKValue) && topKValue > 0)
                options.TopK = topKValue;

            string? ollamaEndpoint = section["OllamaEndpoint"];
            if (!string.IsNullOrWhiteSpace(ollamaEndpoint))
                options.OllamaEndpoint = ollamaEndpoint;

            string? ollamaChat = section["OllamaChatModel"];
            if (!string.IsNullOrWhiteSpace(ollamaChat))
                options.OllamaChatModel = ollamaChat;

            string? ollamaEmbed = section["OllamaEmbeddingModel"];
            if (!string.IsNullOrWhiteSpace(ollamaEmbed))
                options.OllamaEmbeddingModel = ollamaEmbed;

            string? useSqlite = section["OllamaUseSqliteSearch"];
            if (bool.TryParse(useSqlite, out bool ollamaUseSqlite))
                options.OllamaUseSqliteSearch = ollamaUseSqlite;

            string? useAzureSearch = section["UseAzureSearch"];
            if (bool.TryParse(useAzureSearch, out bool azureSearch))
                options.UseAzureSearch = azureSearch;
        }

        // Environment / secrets overrides for Ollama
        string? envEndpoint = configuration[GKeys.OllamaEndpoint];
        if (!string.IsNullOrWhiteSpace(envEndpoint))
            options.OllamaEndpoint = envEndpoint;

        string? envChat = configuration[GKeys.OllamaChatModel];
        if (!string.IsNullOrWhiteSpace(envChat))
            options.OllamaChatModel = envChat;

        string? envEmbed = configuration[GKeys.OllamaEmbeddingModel];
        if (!string.IsNullOrWhiteSpace(envEmbed))
            options.OllamaEmbeddingModel = envEmbed;
    });
}

static GenerellemProfile ParseProfile(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        if (arg.Equals("--profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return ParseProfileValue(args[i + 1]);

        if (arg.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
            return ParseProfileValue(arg["--profile=".Length..]);
    }

    // Clone-and-run default: no cloud credentials required.
    return GenerellemProfile.InMemory;
}

static GenerellemProfile ParseProfileValue(string value)
{
    if (Enum.TryParse(value, ignoreCase: true, out GenerellemProfile profile))
        return profile;

    Console.WriteLine($"Unknown profile '{value}'. Valid values: {string.Join(", ", Enum.GetNames<GenerellemProfile>())}.");
    Console.WriteLine("Falling back to InMemory.");
    return GenerellemProfile.InMemory;
}
