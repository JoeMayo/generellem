using Generellem.DocumentSource;
using Generellem.Embedding;
using Generellem.Embedding.AzureOpenAI;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Repository;
using Generellem.Services;
using Generellem.Services.Qdrant;

using GenerellemConsole;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

CancellationTokenSource tokenSource = new();

IHost host = InitializeConfiguration(args);

// config file location for this demo only
GenerellemFiles.SubFolder = "ConsoleDemo";

ConfigureDB();

await host.RunAsync(tokenSource.Token);

IHost InitializeConfiguration(string[] args)
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.Sources.Clear();

    IHostEnvironment env = builder.Environment;

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
        .AddEnvironmentVariables();

    ConfigureServices(builder.Services);

#if DEBUG
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

void ConfigureServices(IServiceCollection services)
{
    services.AddHostedService<GenerellemHostedService>();

    services.AddTransient<GenerellemContext>();
    services.AddTransient<ILlmClientFactory, LlmClientFactory>();

    services.AddTransient<ISearchService, QdrantService>();
    //services.AddTransient<ISearchService, AzureSearchService>();
    services.AddTransient<IDocumentHashRepository, DocumentHashRepository>();
    services.AddTransient<IDocumentSourceFactory, EnterpriseDocumentSourceFactory>();
    services.AddTransient<IDynamicConfiguration, DynamicConfiguration>();
    services.AddTransient<IEmbedding, AzureOpenAIEmbedding>();
    services.AddTransient<IGenerellemFiles, GenerellemFiles>();
    services.AddTransient<IGenerellemIngestion, Ingestion>();
    services.AddTransient<IGenerellemQuery, AzureOpenAIQuery>();
    services.AddTransient<IHttpClientFactory, HttpClientFactory>();
    services.AddTransient<ILlm, AzureOpenAILlm>();
    services.AddTransient<IPathProviderFactory, PathProviderFactory>();
    services.AddTransient<IRag, GenerellemDefaultRag>();
}
