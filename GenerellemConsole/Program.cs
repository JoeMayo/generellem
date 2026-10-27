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

    ConfigureServices(builder.Services, selectedProfile);

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

void ConfigureServices(IServiceCollection services, GenerellemProfile selectedProfile)
{
    services.AddHostedService<GenerellemHostedService>();
    services.AddTransient<IDocumentSourceFactory, EnterpriseDocumentSourceFactory>();

    services.AddGenerellem(selectedProfile, options =>
    {
        options.SubFolder = "ConsoleDemo";
        ApplyRunMode(args, options);
    });
}

static void ApplyRunMode(string[] args, GenerellemOptions options)
{
    if (!GenerellemRunModeParser.TryReadArgs(args, out GenerellemRunMode runMode, out bool parsed))
        return;

    if (!parsed)
    {
        Console.WriteLine("Unknown --mode value. Use Both, Ingest, or Query.");
        Console.WriteLine("Falling back to Both.");
    }

    options.RunMode = runMode;
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
