using System.Text;

using Generellem.DependencyInjection;
using Generellem.DocumentSource;
using Generellem.Processors;
using Generellem.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

namespace GenerellemConsole;

/// <summary>
/// Starts and runs the main application as a .NET Hosted Service.
/// </summary>
internal class GenerellemHostedService(
    IDocumentSourceFactory docSourceFact,
    IGenerellemFiles gemFiles,
    IGenerellemIngestion generellemIngestion,
    IGenerellemQuery generellemQuery,
    IHostApplicationLifetime lifetime,
    ILogger<GenerellemHostedService> logger,
    IOptions<GenerellemOptions> generellemOptions)
    : IHostedService
{
    /// <summary>
    /// Kicks off file retrieval/upload and starts Console UI.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    public async Task StartAsync(CancellationToken cancelToken)
    {
        try
        {
            Progress<IngestionProgress> progress = new(
                ingestionProgress =>
                {
                    string message =
                        ingestionProgress.CurrentCount > 0 ?
                            $"[{ingestionProgress.CurrentCount}] {ingestionProgress.Message}" :
                            ingestionProgress.Message;

                    logger.LogInformation(message);
                });

            GenerellemRunMode runMode = generellemOptions.Value.RunMode;

            if (runMode != GenerellemRunMode.Query)
            {
                CopyDocumentSources();

                // Normally, this would run in a separate service that
                // runs on a periodic timer to grab the latest files.
                await generellemIngestion.IngestDocumentsAsync(progress, cancelToken);
            }
            else
            {
                Console.WriteLine("Query mode: skipping ingestion.");
            }

            if (runMode != GenerellemRunMode.Ingest)
            {
                PrintBanner(generellemOptions.Value);
                await RunMainLoopAsync(cancelToken);
            }
            else
            {
                Console.WriteLine("Ingest mode: skipping questions.");
            }
        }
        catch (OperationCanceledException opcEx)
        {
            logger.LogError(GenerellemLogEvents.Cancelled, opcEx, "Operation Canceled");
        }
        finally
        {
            lifetime.StopApplication();

            // If the user runs outside of visual studio
            // the application runs and stops before they
            // can see output. Skip when stdin is redirected (CI/scripts).
            if (!Console.IsInputRedirected)
            {
                do
                {
                    Console.WriteLine("\nPress Enter to stop...\n");
                    ConsoleKeyInfo keyInfo = Console.ReadKey();

                    if (keyInfo.Key == ConsoleKey.Enter)
                        break;

                } while (true);
            }
        }
    }

    /// <summary>
    /// Keeps prompting the user for questions until they enter a stop word.
    /// </summary>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns></returns>
    async Task RunMainLoopAsync(CancellationToken cancelToken)
    {
        List<string> stopWords = ["abort", "adios", "bye", "chao", "end", "exit", "quit", "stop"];

        string? userInput;

        Queue<ChatMessage> chatHistory = new();

        do
        {
            Console.Write("generellem>");
            userInput = Console.ReadLine() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userInput) || stopWords.Contains(userInput))
                continue;

            Console.WriteLine();
            StringBuilder streamed = new();
            await foreach (string token in generellemQuery.AskStreamAsync(userInput, chatHistory, cancelToken))
            {
                streamed.Append(token);
                Console.Write(token);
            }

            string response = streamed.ToString();
            ChatHistoryRecorder.RecordTurn(chatHistory, userInput, response);
            Console.WriteLine();
            Console.WriteLine();

            if (cancelToken.IsCancellationRequested)
                break;

        } while (!stopWords.Contains(userInput));
    }

    static void PrintBanner(GenerellemOptions options)
    {
        const ConsoleColor bannerColor = ConsoleColor.Magenta;
        const ConsoleColor messageColor = ConsoleColor.Cyan;
        const ConsoleColor warnColor = ConsoleColor.Yellow;

        PrintColoredText("*-----------------------------------------*", bannerColor);
        PrintColoredText("* Welcome to the Generellem Console Demo! *", bannerColor);
        PrintColoredText("*-----------------------------------------*", bannerColor);

        Console.ForegroundColor = messageColor;
        Console.WriteLine();
        Console.WriteLine($"Profile: {options.Profile}");
        Console.ResetColor();

        if (options.Profile is GenerellemProfile.InMemory or GenerellemProfile.Sqlite)
        {
            Console.ForegroundColor = warnColor;
            Console.WriteLine($"{options.Profile} profile — local demo only. Answers are extractive quotes from your documents, not a chat model.");
            Console.ResetColor();
        }

        if (options.Profile == GenerellemProfile.Azure)
        {
            Console.ForegroundColor = messageColor;
            Console.WriteLine(options.UseAzureSearch
                ? "Search backend: Azure AI Search."
                : "Search backend: Qdrant. Set Generellem:UseAzureSearch to true to switch.");
            Console.ResetColor();
        }

        Console.ForegroundColor = messageColor;
        Console.WriteLine(@"
This Console app lets you try out Generellem to see how it works. You can
also use this to test new services like RAG Search and new LLMs.

You can start out with a question like, ""How do I contribute to Generellem?"".

Generellem uses content from this repository to answer questions. The root 
folder has Markdown files, like CONTRIBUTING.md. Also, the Documents folder 
has a growing list of content, covering supported scenarios. For testing, 
you can browse those documents and form questions based on their content.

Profiles:
  --profile InMemory  (default) zero setup, extractive answers
  --profile Sqlite    persisted vectors, extractive answers
  --profile Ollama    local chat model (requires Ollama)
  --profile Azure     Azure OpenAI + Qdrant, or Azure AI Search when UseAzureSearch is true

Modes:
  --mode both     ingest, then ask questions (default)
  --mode ingest   ingest documents and exit
  --mode query    ask questions without ingesting

Set Generellem:RunMode in appsettings.Local.json when you omit the flag.

Quick start: https://github.com/JoeMayo/generellem/blob/main/README.md#try-it

Let's get started!");
        Console.ResetColor();
    }

    /// Helper function to print text in a specific color
    static void PrintColoredText(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Copies document source configuration files to AppData.
    /// </summary>
    /// <remarks>
    /// Ingestion reads Document source files, like FileSystem.json and Website.json
    /// from the user's AppData folder. This is Microsoft's recommended practice for
    /// managing artifacts that support an app. It's also useful because installing
    /// an app update doesn't delete those files.
    /// </remarks>
    void CopyDocumentSources()
    {
        IEnumerable<IDocumentSource> docSources = docSourceFact.GetDocumentSources();

        string appDataPath = gemFiles.GetAppDataPath();

        Directory.CreateDirectory(appDataPath);

        foreach (IDocumentSource docSource in docSources)
        {
            string configFileName = $"{docSource.GetType().Name}.json";

            if (File.Exists(configFileName))
                File.Copy(configFileName, Path.Combine(appDataPath, configFileName), overwrite: true);
        }
    }
}
