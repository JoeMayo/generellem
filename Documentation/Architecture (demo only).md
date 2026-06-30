## What is Generellem?

>Note: Please visit [Architecture](https://github.com/JoeMayo/generellem/wiki/Architecture) in the wiki for the official version of the architecture. I put this here for testing a long document during the ingestion process.

Generellem is a service that lets anyone use their own data with a Large Language Model (LLM). It comes as 2 products:

1. An open-source product. This lets developers join a community, contribute to the project, and use the software themselves. If you're interested in using the open source, check out the GitHub repository and join in.
2. A commercial SaaS product. This allows companies to quickly use the service and have a subscription for help and support. If you're interested in the commercial offering, visit the [Generellem Website](https://generellem.ai/) and sign up.

>Note: This is a new project and will evolve as the community increasingly participates and customers provide feedback.

## Characteristics

Characteristics are the guiding principles of the architecture. There are many potential characteristics. However, the ones listed here have a strong influence on how the entire project evolves.

### Adaptability

Generellem is essentially a Plug-In architecture, which by its nature requires adaptability. The core system uses Inversion of Control (IoC) to specify interchangeable dependencies to system services. The system will have built-in services for search, Retrieval-Augmented Generation (RAG), and LLMs. However, those are based on interfaces that anyone can implement with their own service. In time, Generellem will grow and people can contribute new service implementations. Adaptability is built-in from day 1.

### Maintainability

With a lot of potential contributors and a continuously growing code base, maintainability becomes ever more important. Developers need a way to approach the code and find what they need, fix bugs quickly, and easily add new features. Here are a few concepts that contribute to that:

1. Coding standards
2. Reviews
3. SOLID Principles
4. Minimizing technical dept

While no code base is perfect, making maintainability a first-class characteristic sets a North star to follow.

### Reliability

The quality has to be top-notch and the software can't crash. Although there will be times when we have issues with 3rd party service availability, network, or infrastructure issues (things that are always unpredictable), the Generellem software itself shouldn't crash. We can accomplish this by using proven techniques to manage quality:

1. Unit tests
2. Integration testing
3. Using tools like Polly for resiliency
4. Document potential exceptions

That said, Generellem is a library that other applications use. If Generellem throws an exception, it's up the consuming application to handle those exceptions.

### Other Characteristics

There are many more characteristics that could be addressed. However, many are enterprise features that are external to the core Generellem library. For example, scalability can be achieved through cloud deployments. You can have security by providing secure configuration for keys. Other characteristics, such as usability aren't applicable to a class library.

>Note: These things can change in time and we should be open to new ideas.

Now that you have an idea of the system characteristics, let's take an overview of the entire system.

## Overview

Generellem is a library that you can use in your own applications. It allows you to specify your own documents that serve as source data to an LLM. The following diagram is an overview, from a birds-eye view, on how the major components fit together to make Generellem work:

![Overview](https://github.com/JoeMayo/generellem/assets/6180567/6d9dacf1-2ae6-441f-9d37-04a61b7c41fd)

At the bottom of the diagram is your application. It references the Generellem library. For access to local file systems or internal network resources, Generellem will have to run on a server that has access to those files and resources. If the documentation is accessible via the cloud, Generellem can run via a cloud service. 

In the middle is the NuGet package, which you would have a package reference to in your project. Whenever you reference NuGet, it will also pull in a list of dependencies. While we don't intend to have a lot of dependencies, there is a minimal number necessary for demos and basic implementation. The default implementation is based on Azure OpenAI and Azure Search. Generellem is a cross-platform library, allowing it to be used on Linux, MacOS, and Windows.

Generellem makes network calls to external providers, which include documents, search, and LLM services. These can be your internal resources, external servers, or cloud services. Generellem has connector interfaces, where anyone can build connectors that work with any external provider.

That was a high-level overview of how the big pieces fit together. Next, we drill down into Generellem itself to examine interfaces and components that make it work.

## Components

Generellem components are those parts of an application inside of the Generellem library, labeled as NuGet, and the parts that support external services, labeled as providers, shown in the last section's overview. Here, I'll dive into what those components are and explain their interactions.

The first concept from the component view is that there are two separate groups of components: those that process files and those that process user requests. 

### Processing Documents

There are two primary types of components for working with documents: Document Sources and Document Types. A Document Source is a place where the document comes from, such as a file system or web site. Document Types are documents that have a specific format or purpose, such as an Excel spreadsheet or a PDF file. The image below shows how components for Document Sources and Document Types are modeled in Generellem.

![DocumentComponents](https://github.com/JoeMayo/generellem/assets/6180567/d34722e0-890e-4f68-9cdb-ca598ab59e1a)

In the diagram, `IDocumentSource` represents anything that is a Document Source. It's `GetDocumentsAsync` method returns a collection of `DocumentInfo` that has metadata and a `Stream` for reading the document. While the initial implementation of a document is assumed to have a textual representation, we want to leave open the possibility of other document types in the future, like audio, video, or any other binary representation of information.

Next, notice the aggregation of Document Types, represented by the `IDocumentType`. As you can see, I listed just a few of the many document types that are possible. The `GetTextAsync` method allows each `IDocumentType` implementation to read the `DocumentInfo` `Stream` and translate the contents to text. `CanProcess` is a flag that lets me put in placeholder that isn't fully supported yet and might be removed in the future (maybe feature flags?). `SupportedExtensions` indicates which file extensions we're able to process at this time. e.g. *.doc and *.docx represent different versions and formats for MS Word documents and since we have a cross-platform requirement we can't use Windows-specific libraries to process *.doc files. This can change in the future and we can update `SupportedExtensions` so if it eventually supports *.doc files. The point is that Generelem scans a Document Source and only processes the type of files and extensions that it's able to, ignoring everything else until supported.

In addition to documents, Generellem has components that help work with user questions, which we discuss next.

### Processing User Questions

At the core of handling user questions is a request and a response, respectively sending and receiving text data. Because we have a requirement to support multiple LLMs, we need to abstract the handling of the request and response, which differs between LLMs. The following figure shows how we do it:

![UserRequestResponse](https://github.com/JoeMayo/generellem/assets/6180567/66fb289a-acbc-4e4e-b17c-a4161e7c2850)

The `IChatRequest` and `IChatResponse` represent the request and response in the simplest way possible. They just have a `Text` property of type `string`. As of this writing, Generellem supports Azure Open AI, as you can see via the `AzureOpenAIChatRequest`, implementing `IChatRequest`, and `AzureOpenAIChatResponse`, implenting `IChatResponse`. In addition to the required `Text` property, these classes have members specific to Azure Open AI: `AzureOpenAIChatRequst` has a `ChatCompletionsOptions` property for special configuration options that Azure Open AI needs and `AzureOpenAIResponse` has a `ChatCompletionsResponse` for the response options from Azure Open AI.

Now that you've seen the major components in Generellem, let's discuss their interactions and important processes.

## Processes

There are two main processes for Generellem: ingestion and querying. Ingestion is the process of scanning for documents and saving them in a format to query later. Querying is the process that searches for saved documents and builds a custom prompt for an LLM. We'll discuss these in the next two sections.

### Ingestion

Earlier, you learned about Document Sources and Documents. To support the ability to use AI with your own documents, we need to ingest the documents. This ingestion process transforms the document and saves it in a vector DB. It's part of Retrieval Augmented Generation (RAG). Here's a quick overview of the main steps of the process:

1. Search each Document Source.
2. In each Document Source, search for Documents that we're able to process.
3. Prepare each Document for storage in the vector DB.
4. Save the Document in the vector DB

The following diagram shows how Generellem does this:

![IngestionProcess](https://github.com/JoeMayo/generellem/assets/6180567/63465687-25c8-495b-91b0-6d714f3a8c99)

Ideally, the `DocumentProcessor` runs in its own background process on a timer. That way, it can wake up periodically to see if there are any new, modified, or deleted documents to handle. For simplicity, this diagram doesn't show the loops on the Document Source and Documents. It also only shows the new document process, though Generellem does handle modified and deleted documents.

The first step is getting all of the supported Document Sources, which is the job of the `DocumentSourceFactory`. So far, we have support for `FileSystem` and `WebSite` and will be adding more. This returns all of the Document Sources, which we'll iterate through.

>Note: Behind the scenes, Generellem populates a SQLite DB with document metadata to help keep track of what documents are new, modified, or deleted. One aspect of this is that you'll want to ensure you have enough file space if you have a lot of documents. The size of each file is defined by a `DocumentHash` model that has an `int`, `string` with file hash, and another `string` with a unique identifier for the file called a `DocumentReference`.

Next, we get all of the supported documents, each represented by a `DocumentInfo` with all of the things we need to process that document. One of those things is an `IDocumentType` that's specific to each document. Here, you can see how this design creates an abstract representation of Document Sources and Document Types, allowing a single algorithm to operate on all of them the same way. The specifics are isolated to the instances of Document Source or Document Type.

So far, we're assuming that everything we're working with is text. Although that could change in the future, the current approach leaves that as a refactoring to do when and if that happens. There will certainly be fun and interesting discussions. So, the next step is to call `GetTextAsync` on the `IDocumentType`, whose specific implementation knows what type of document it's working with and can extract plain text.

Once we have the text, we'll need to transform it once again in order to save it in a vector DB. As the DB type name suggests, we need to translate the text into a vector of numbers. So, we call the `EmbedAsync` method on `RAG`, which is a property holding an `IRag` implementation that knows how to work with the mechanics of the RAG process. Another term for translating text into vectors is _embedding_, which is where the _Embed_ in `EmbedAsync` comes from. Before embedding any string, `EmbedAsync` breaks the text up into smaller pieces as part of a `TextChunk` that contains the vector representation of a piece of text. This is part of the RAG process that manages the size of documents and improves results of user queries.

Finally, we need to store the document (transformed to `TextChunk`) in the vector DB. We currently support Azure Cognitive Search nd have plans to support many more vector DBs. So, we call `IndexAsync` to save the chunked document.

Once we have documents ingested, we can start querying.

### Querying

So far, we've defined components and described the ingestion process, where documents are processed and saved into a vector DB. Everything is now in place to start querying. Rather than just send the user's query to the LLM, we're going to process it and match it with documents first to establish context. Here are the steps:

1. Clarify the user's query in the context of recent queries.
2. Search for relevant documents.
3. Query the LLM with the documents as context.

The following diagram shows how this process works:

![QueryProcess](https://github.com/JoeMayo/generellem/assets/6180567/b2ea7646-dff0-486b-99f7-76265789d7e8)

The `AzureOpenAIOrchestrator` manages the RAG process that deals with querying the LLM. It has a `chatHistory`, which is a list of previous queries and their answers.

When the user sends their query, `AzureOpenAIOrchestrator` takes that query along with `chatHistory`, to establish context, and instructions to the LLM to clarify the user's true intent, based on the context. This results in a new query that should better represent the user's intent.

Based on the new user intent, we call `SearchAsync` on the `RAG` instance. This searches the vector DB, where ingestion stored documents, for documents matching the user intent query. This returns a list of documents that match the query.

>Note: If you recall from the ingestion process, all documents are chunked. Therefore, what we really get back are chunks of documents that match user intent.

We now have enough information to query the LLM. The documents (text chunks) set context and we call `AskAsync` on the `LLM`, using those documents to answer the question. The result is that the answer is based on your data and not some unrelated hallucination from whatever the LLM was trained on.

The last thing to do is to take the new response and update `chatHistory`, setting context for the next query.

## Deployment

Generellem will be a .NET 8 DLL and you'll be able to get it via NuGet, GitHub release page, or source code. Right now, it's only available as source code, which will change after a few more preparatory steps. Follow the guidance in [Adding Generellem to Your App](https://github.com/JoeMayo/generellem/wiki/Adding-Generellem-to-Your-App) for ideas on how to incorporate it into your app.

## Governance 

I haven't decided what will go in this section exactly. I'm thinking along the lines of change notes for things that change the architecture significantly. Also, we'll need to specify why we did things certain ways so we can go back and understand what the evolution of the project is. For day-to-day operations, we have a [Generellem Project](https://github.com/users/generellem/projects/2) to track tasks.

This article focuses on the ConsoleDemo project in the cloneable source code. Specifically, refer to the following files for the full code listings:

[Program.cs](https://github.com/JoeMayo/generellem/blob/main/GenerellemConsole/Program.cs)

and

[GenerellemHostedService.cs](https://github.com/JoeMayo/generellem/blob/main/GenerellemConsole/GenerellemHostedService.cs)

## Program.cs is `Main`

The applications starts in _Program.cs_. Here are the primary tasks we'll cover:

* Initializing the configuration
* Setting up the Inversion of Control (IoC) container
* Configuring the SQLite DB

> Follow the directions in [Getting Started](https://github.com/JoeMayo/generellem/wiki/Getting-Started) to understand how to run _ConsoleDemo_.

### Initializing the configuration

When we talk about configuration, we mean where you get configuration information like connection strings, service keys, and any changeable piece of data that you don't want compiled into the code. For _ConsoleDemo_, we need _appSettings.json_, environment variables, and user secrets. Here's the `InitializeConfiguration` method, explaining the approach:

```C#
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

```

We're using the [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host?tabs=appbuilder), `HostApplicationBuilder`, which supports configuration, IoC, and app lifetime, to name a few capabilities. The `Configuration` property lets us add any type of configuration we want. In this example, we're using environment specific _appSettings_ as well as the ability to read configuration from the machine environment. What's important here is that this gives you the ability to add the configuration that you want and it works natively with Generellem via `IConfiguration`. For example, the code uses [.NET User Secrets](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/secure-net-microservices-web-applications/developer-app-secrets-storage), via `AddUserSecrets<Program>()`, for Azure service keys and passwords to keep them out of the development environment. In your Release deployments, you can use another configuration provider for these secrets.

In addition to the `Configuration` property, we use the `Services` property of `HostApplicationBuilder`, discussed next.

### Setting up the IoC Container

The `HostApplicationBuilder.Services` property exposes functionality to support IoC, the ability to specify dependencies to the code, rather than having the code explicitly instantiate types. The benefit of this is that the code is loosely coupled, easier to maintain, and makes testing via mocking simpler. The loosely coupled concept is a particularly important feature because it enables Generellem's [Plug-In Architecture](https://github.com/JoeMayo/generellem/wiki/Architecture). I'll expand on that briefly, after we see the code:

```C#
void ConfigureServices(IServiceCollection services)
{
    services.AddHostedService<GenerellemHostedService>();

    services.AddTransient<GenerellemContext>();
    services.AddTransient<GenerellemOrchestratorBase, AzureOpenAIOrchestrator>();
    services.AddTransient<LlmClientFactory, LlmClientFactory>();

    services.AddTransient<IAzureSearchService, AzureSearchService>();
    services.AddTransient<IDocumentHashRepository, DocumentHashRepository>();
    services.AddTransient<IDocumentSourceFactory, DemoDocumentSourceFactory>();
    services.AddTransient<IHttpClientFactory, HttpClientFactory>();
    services.AddTransient<ILlm, AzureOpenAILlm>();
    services.AddTransient<IRag, AzureOpenAIRag>();
}
```

The `InitializeConfiguration` method calls `ConfigureServices(builder.Services)`, passing in the `Services` property instance, which is an `IServiceCollection` parameter, named `services`. Any types you add to `services` can be injected into the Generellem code via IoC. Essentially, this gives you the ability to plug-in any types you want, whether part of Generellem or your own custom implementation. The following table explains what each of these items are.

| Type | Purpose |
|--|--|
| GenerellemContext | Entity Framework (EF) DBContext for the SQLite DB. |
| GenerellemOrchestratorBase | Business logic for managing interactions between Retrieval Augmented Generation (RAG) and Large Language Model (LLM). It also has logic for DocumentSource ingestions (Note: we have a task to refactor this and separate these two concerns. |
| LlmClientFactory | Interface for creating LLM library instances. There isn't a guarantee that LLM libraries will have their own interfaces or even be testable and his helps abstract that and still have the ability to do unit tests. |
| IAzureSearchService | This is using Azure Cognitive Search as a Vector DB, supporting the RAG process. |
| IDocumentHashRepository | We use an object called a `DocumentHash` to determine whether a document has changed since the last time we saw it. This repository is for DB CRUD operations on the _DocumentHash_ table. |
| IDocumentSourceFactory | Generellem supports any type of `IDocumentSource`, which defines a place where we find documents. Examples are `FileSystem` for Servers and LAN file systems and `Website` for an internal Web application. |
| IHttpClientFactory | We need to use `HttpClient` for communicating with Web artifacts and this lets us build unit tests without explicit `HttpClient` dependencies. |
| ILlm | This is the Large Language Model (LLM), such as Azure OpenAI. It provides the AI we use to respond to the user. |
| IRag | The Retrieval Augmented Generation (RAG) process is instrumental in getting an LLM to understand your documentation. |

You can use this code as-is and get value out of it right away. However, what this gives you is choice. Here are a few examples:

* Instead of using SQLite, you can use your own DB
* Instead of Cognitive Search, you can switch to another Vector DB
* Instead of Azure OpenAI, you can use Google Bart or Meta Llama

In fact, one of the goals of Generellem is to be a place to expand on all of these choices. We'll be adding more plug-ins in the future and welcome contributions from you. See the [Contributing](https://github.com/JoeMayo/generellem/blob/main/CONTRIBUTING.md) docs for more information.

Another part of the initialization process is a SQLite DB, which we'll discuss next.

### Configuring the SQLite DB

We wrote the SQLite code so that you could just run the demo without thinking about it. We also used Entity Framework (EF), which has Migrations to auto-configure schema. Here's the code:

```C#
void ConfigureDB()
{
    using GenerellemContext ctx = new();

    ctx.Database.OpenConnection();
    ctx.Database.Migrate();
}
```

When instantiating `GenerellemContext` we want to dispose, with the `using` statement, the instance so we don't unnecessarily hold onto a DB connection. As you might guess, the `Database` property gives you the ability to manipulate the DB. `OpenConnection` is a little bit subtle because it also creates the DB if it doesn't already exist. This happens because the default connection string `Mode` parameter is `ReadWriteCreate`. You can verify this by peeking at the code in [`GenerellemContext`](https://github.com/JoeMayo/generellem/blob/main/Generellem/Repository/GenerellemContext.cs).

Looking at the _Generellem_ project code, you'll also see an EF Core _Migrations_ folder. The call to `Migrate` uses the code in that folder.

That's how the app starts and the details of _Program.cs_. One of the statements you'll see at the top of _Program.cs_ is `await host.RunAsync(tokenSource.Token)`. You might wonder, "What is it running?". You might also wonder why we didn't list `GenerellemHostedService` in the table for services added in `ConfigureServices`. It's because there's a lot of interesting code in there that you'll need to know about and I dedicated an entire section to it, which you'll learn about next.

## `GenerellemHostedService` is the Driver

`GenerellemHostedService` implements `IHostedService`, which specifies `StartAsync` and `StopAsync` methods. Therefore, when the initialization code calls `host.RunAsync`, it calls `StartAsync` on `GenerellemHostedService` which is where we do two things: Ingest documents and let the user interact with the service. Here's the `StartAsync` method with some items removed to simplify this discussion:

```C#
    public async Task StartAsync(CancellationToken cancelToken)
    {
        await orchestrator.ProcessFilesAsync(cancelToken);

        PrintBanner();

        await RunMainLoopAsync(cancelToken);
    }
```

As you can see, we process files (the ingestion process), print a banner to the user with an informative message, and then run a loop to interact with the user. Let's discuss document ingestion first.

### Document Ingestion

The first statement inside of `StartAsync` to `ProcessFilesAsync` kicks off the ingestion process. This scans a document source, looking for supported documents to ingest into Generellem. These are the documents that you want Generellem to use in answering questions. You specify which documents in 
a configuration file for the document source: _FileSystem.json_ for the `FileSystem` document source and _Website.json_ for the `Website` document source. Here are the examples in Generellem:

File System:

```JSON
[
  {
    "path": "..\\..\\..\\.."
  }
]
```

This is a JSON array of objects with `path` properties - one `path` property per object. This particular example assumes that you're running the app from an IDE, like Visual Studio, and you want to scan all of the files in the project. The Generellem project includes a _Documents_ folder with several supported document types. You can add as many path objects as you want to specify which files to ingest. The scan is recursive, so any directories/folders below the path are scanned.

Website:

```JSON
[
  {
    "url": "https://github.com/JoeMayo/generellem/wiki"
  }
]
```

This is a JSON array of objects with `url` properties - one `url` property per object. This example scans the Generellem Wiki on GitHub. You specify the top-level URL and Generellem crawls the site recursively, treating each page as a separate document.

> Note: The current Website Document Source makes the assumption that the Website doesn't require login. We have an [issue](https://github.com/JoeMayo/generellem/issues/114) to design extensibility points to address this. This will require some research and we appreciate feedback on what a good approach would be.

When the app runs, you'll see documents being ingested. You can verify this by visiting the Azure Cognitive Search resource that you configured during the [Getting Started](https://github.com/JoeMayo/generellem/wiki/Getting-Started) process and visiting the index named `generellem-index`. Here's what it might look like for you:

![ConsoleDemoStartup](https://github.com/JoeMayo/generellem/assets/6180567/9296c561-3404-4ac2-b346-a78f1359b3ea)

If we've already ingested a document, we don't ingest it again unless it has changed. That's what the SQLite DB `DocumentHashes` table is for - reducing the extra network overhead to optimize the ingestion process. Another task that `DocumentHashes` helps with is identifying previously ingested documents that have been deleted since the previous ingestion and deleting them from Azure Cognitive Search so that your index doesn't collect stale data.

The `StartAsync` method then calls `PrintBanner`, which you can observe in the image.

Finally, `StartAsync` calls `RunMainLoopAsync`, which we'll discuss next.

### Interacting With The User

As it's named, the `RunMainLoopAsync` method continuously loops, letting users ask a question and get an answer per iteration. Here's the code:

```C#
    async Task RunMainLoopAsync(CancellationToken cancelToken)
    {
        List<string> stopWords = ["abort", "adios", "bye", "chao", "end", "quit", "stop"];

        string? userInput;

        Queue<ChatMessage> chatHistory = new();

        do
        {
            Console.Write("generellem>");
            userInput = Console.ReadLine() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userInput))
                continue;

            string response = await orchestrator.AskAsync(userInput, chatHistory, cancelToken);

            Console.WriteLine($"\n{response}\n");

            if (cancelToken.IsCancellationRequested)
                break;

        } while (!stopWords.Contains(userInput));
    }
```

The loop ends when the user enters one of the `stopWords`. 

The `chatHistory` queue is important here because Generellem uses that to create context for the current query. The assumption here is that the LLM will return more accurate results if it has the current history of questioning. It also assumes that history is related to the current question. We don't know that for sure because what if the user bounces around on topics, resulting in answers grounded in old context. To resolve that, you can add your own mechanism to clear history - maybe a "New Topic" button in your UI that clears the Queue, removing old context. There are a couple of categories of tradeoffs here: 

* Less context for potentially lower quality responses vs. more context for higher quality answers.
* Lower quality for unrelated context vs. high quality for relevant context.

>Note: Generellem currently manages this queue manually, capping it at the 5 most recent queries. We have an [issue](https://github.com/JoeMayo/generellem/issues/70) open to refactor this. We will probably need some type of extensibility point to give you maximum flexibility in how to manage history because the strategy is likely to not be the same for different users. Your thoughts here are welcome.

Finally, we prompt the user, send the request via `AskAsync`, and print the response. Inside of `AskAsync` is where the magic happens. We're using a technique called Retrieval Augmented Generation (RAG). In a nutshell, the RAG process uses the ingested documents, builds a sophisticated prompt with context and documentation, and sends that to the Large Language Model (LLM). Besides history, the mechanics of this is something you don't need to dive into to get started. However, because of extensibility, you could write your own `IRag` plugin to manage the process too. As we go along, we'll add more plugins and continuously tweak the RAG processes to optimize Generellem quality.

That's it, just call `ProcessFilesAsync` to ingest documents and `AskAsync` to get an LLM response.

## Summary

This article explains how to integrate Generellem into your application. Remember to follow the guidance in [Getting Started](https://github.com/JoeMayo/generellem/wiki/Getting-Started) to set up your environment and resources.

The initialization process relies on the .NET `HostApplicationBuilder`, which we used for configuration and IoC. You have the ability to plug-in your own configuration and Generellem uses the `IConfiguration` interface to acces all configuration artifacts. Generellem also uses IoC, rather than direct type instantiation, to work with objects. This gives you the ability to replace components as you like, makes the application more maintainable, and supports the type of unit testing we're doing with mocking libraries.

After initialization, there are two major concepts to working with the library: ingesting documents and processing user input. This was a demo that included the ingestion process for every time the application runs. However, you can separate that into another background process that runs on a timer, separate from your app. All you need to do after that is collect a query from the user, call `AskAsync`, and show the response to the user. You might want to offer some mechanism for managing the history, which is a parameter for `AskAsync`, to clear out the context that Generellem constructs when communicating with the LLM.

On a final note, Generellem is still early. We're getting it out there as soon as possible so we can get feedback from people like you. Commenting on issues, participating in discussions, or just [DMing me on X](https://twitter.com/JoeMayo) is fine. We would love to here how you're using it and what you think. 

# Contributing

There are developers who maintain Generellem and will continue to do so. However, the project is open to participation from anyone that wants to make a contribution. This guide discusses items that everyone should be aware of in the contribution process. We'll discuss different ways to contribute, coordination process, and how to submit code.

## Types of contributions

There are many ways to contribute such as code documentation, and helping others. The following sections discuss this more.

### New Features

There are two way to see existing new features: [The Generellem Project](https://github.com/users/generellem/projects/2) or [Issues](https://github.com/JoeMayo/generellem/issues). By design, we turn all new project cards into Issues so they show up in the Issues too, where it might be easier for some people to read details. Of course, reading via the Project gives a good idea of each issue's status.

If there's an issue that you would like to work on, reach out to Joe so he can assign that to you. If you don't see a feature that you would like to have, either create a new one in Issues or visit the Discussions section if you want to discuss it with other community members first.

Really, we need at least some type of governance over new issues. Coordinating these things helps people from wasting a lot of time going in a direction that doesn't fit the project. Communicating about it is better. Also, it keeps multiple people from working on the same feature at the same time.

If you're a new developer, look for the issues tagged as "good first issue". Of course, if you just want to jump in head first go-for-it. Please coordinate and ask for help so you don't flounder too long on a single issue. We want to see you succeed.

### Bug Fixes

In many ways, the bug fix process is similar to New Features in that we want to coordinate and track each fix. Keeping track of bugs also a metric that helps measure the health and quality of the system. Please search open and closed issues to see if the bug has already been addressed before submitting a new issue.

### Unit Tests

You can run code coverage on Generellem test and might find gaps that we should have written a unit test for. Similar to New Feature, please coordinate. You want to avoid writing a unit test in code that someone else is currently working on, where they would normally be expected to write those tests.

### Documentation

There are two main areas of documentation: Markdown in source code and the Wiki. The Markdown files are like this fine, CONTRIBUTING.md. They're located and named to fit with GitHub conventions. e.g. by placing them in the root folder, GitHub can automatically link to them through the project's main page right-side menu. The Wiki is a growing repository of documentation on architecture and developer documentation.

You're welcome to update the documentation in any way that would improve it. Even spelling corrections and grammar are fine. The standard is US English so we don't accidentally change back and forth between other forms. For the source code Markdown, you'll need to submit a PR, explained below. For the Wiki, you're welcome to do small spelling or gramatical improvements as you need. However, if you want to do an extensive change, please coordinate via Discussion or Issues.

>Important! All documentation must be original - not AI generated. The rationale is that Generellem uses the documentation as proof of concept of how it works by making non-public data consumable. AI generated data pollutes the data source.

### Helping Others

There are various ways to help others. The first is by participating in Issues and Discussions. You can ask questions, answer other people's questions, or just generally participate in any discussion - as long as it's on topic for Generellem. New developers might want to take on an Issue and might need help. In other cases, there might be a new feature of such significant size the people might want to form a team to coordinate the work. The sky is the limit here and everything that you and others do contributes to the community and helps you achieve whatever goals you set out for yourself.

## Handling Issues

Everything is documented on an Issue. Although the Projects feature creates a card, we should change cards to Issues, which have more options like status, tagging, and conversations. It's also advantageous that an Issue appears in both the Projects and Issues pages, making them more visible to people who might not look in another location.

The conversation about an Issue should happen on the issue, rather than Discussions, email, or anywhere else. That creates a history of what we were thinking at that point in time, which matters because people usually have questions about why something was done a certain way. We can look back in time and either see an explanation or make assumptions about what technology might have been most appropriate at that point in time.

When you take an Issue, take ownership of it. This means you ensure you are assigned, the status is up-to-date, all fields are properly updated, and any other information about that issue is recorded. As aluded to in the previous paragraph, the issue is a historical record of what we were thinking at the time and why we made the decisions we did.

## Submitting Code

Let's say you found something you want to work on, you coordinated the decision, and you have the code written. The next step would be to submit that codes to it goes into the repository. The `main` branch is locked, so you can't just check into it. This is intentional to prevent people from accidentally checking in breaking changes. The following items outline a safer approach:

1. You need to have a separate branch to work on. If you accidentally started working on the `main` branch, use Git to figure out how to get your changes into a new branch. If you're using Visual Studio 2022, this is easy because if you have files in your current branch, when you create and checkout a new branch, Visual Studio will ask if you want to move your changes to the new branch.
2. Write Unit Tests. You can look at the test project source code to pick up the style of test being used. We use XUnit as the test framework and Moq for a mocking library. A PR without unit tests is unlikely to be approved until tests are written. Unit tests are part of the code.
3. Push your branch and create a new PR. The PR has a title, description, and Issue reference. The title is a short description of what the PR is about. The description explains the work that was done. Imagine you are a developer looking at file history and need to read the details of a commit - what would you appreciate to read? The expectation that you've coordinated the work assumes that there is an Issue associated with the code. You should reference the Issue with the hashtag, such as in #42, where the hashtag says you're referencing an issue and 42 is the issue number.
4. PR is an acronym for Peer Review. This means that you shouldn't automatically merge without a second review. Code reviews are a best practice in software engineering. They help by identifying problems, sharing knowledge, and maintaining consistent project standards. We'll try to be responsive to PRs and make sure they don't linger too long. If someone hasn't reviewed your PR within a couple of days, bump it in discussion, issue, or just reach out to someone as a reminder. BTW, smaller PRs are much easier for people to review. If you have a giant PR, it can take a long time to review. That said, all code that gets checked in must work, which is another benefit of writing unit tests.
5. After someone approves your PR, you can merge. You should use a squash merge, which reduces the amount of commit noise in history. You should delete your branch after merging so we don't have a bunch of stale branches cluttering the repository.

## Summary

This article started out by welcoming anyone who would like to contribute to Generellem. While a lot of people believe that contributing to open source is primary code, remember that there are many ways to contribute, such as working on documentation and various ways to help other people. After you've written code or modified documentation that is part of the source code, you should submit a PR. This document explained the process to submit code via a PR and explained why certain steps are important. Finally, remember that this all improves with community participation and constructive feedback is welcome.