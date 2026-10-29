![Generellem Logo](images/dp_social.png)

### Generellem

Generellem is an opinionated framework for implementing Retrieval Augmented Generation (RAG) processes. Essentially, you can use AI on your own documents.

#### Project Status:

Generellem is now a v1.0 released product. If you find a bug, create a new [Issue](https://github.com/JoeMayo/generellem/issues).

The framework is cross-platform. We target the latest .NET LTS release, which is .NET 10. The initial releases are building around Azure and Windows technologies and we'll expand to other platforms and technologies as we go. BTW, the demos already run on Linux, macOS, and Windows.

#### Quick Start (no Azure, no secrets)

Clone the repo and run the console demo. The default **InMemory** profile works immediately:

```bash
git clone https://github.com/JoeMayo/generellem
cd generellem/GenerellemConsole
dotnet run
```

When the prompt appears, try:

```text
generellem> How do I contribute to Generellem?
```

Type `quit` to exit.

| Profile | Command | Requirements |
|---------|---------|--------------|
| **InMemory** (default) | `dotnet run` | None — extractive answers from local docs |
| **Sqlite** | `dotnet run -- --profile Sqlite` | None — vectors persist in SQLite |
| **Ollama** | `dotnet run -- --profile Ollama` | [Ollama](https://ollama.com) + `ollama pull llama3.2` + `ollama pull nomic-embed-text` |
| **Azure** | `dotnet run -- --profile Azure` | Azure OpenAI + Qdrant keys (user secrets). Set `Generellem:UseAzureSearch` to `true` to use Azure AI Search instead |

Optional Docker helpers for Qdrant/Ollama:

```bash
docker compose up -d
```

The console can ingest, answer questions, or both:

```bash
dotnet run -- --mode ingest
dotnet run -- --mode query
dotnet run -- --mode both
```

Configure local options in `GenerellemConsole/appsettings.Local.json` (chunk size, overlap, TopK, temperature, `SummarizeUserIntent`, `EmbeddingBatchSize`, `RunMode`, Ollama models). `RunMode` is `Both`, `Ingest`, or `Query`. The `--mode` flag overrides it.

Search fuses vectors with keywords: Azure AI Search hybrid queries, Qdrant sparse vectors, and SQLite FTS5. `Generellem:RerankMode` is `Lexical` (default), `Llm`, or `None`. The console writes the answer as it streams. Image files (`.png`, `.jpg`, `.gif`, `.bmp`) are ingested from embedded text and the file name. Set `EnableImageVision` and `VisionModel` to also ask a vision model. SQLite uses the bundled sqlite-vec index when the host can load it.

Releases use semantic versions. Push a `vMAJOR.MINOR.PATCH` tag to pack Generellem and publish a GitHub release.

#### Getting Started

There's a growing list of demos that you can clone and run to get a feel for how the framework works. You might want to start with [GenerellemConsole](https://github.com/JoeMayo/generellem/tree/main/GenerellemConsole) which has foundational support. We'll have more demos as the technology support grows. e.g. [MSGraphDemo](https://github.com/JoeMayo/generellem/tree/main/MSGraphDemo) shows how to work with OneDrive via Microsoft Graph.

In application code, register a profile with one call:

```csharp
services.AddGenerellem(GenerellemProfile.InMemory);
// or GenerellemProfile.Sqlite / Ollama / Azure
```

We have a growing library of documentation to include Architecture, Getting Started, and more. Please visit the [Wiki](https://github.com/JoeMayo/generellem/wiki) for more information.

#### Contributing

Generellem is an open-source project and we welcome your contributions. Please visit the [Contributing](https://github.com/JoeMayo/generellem/blob/main/CONTRIBUTING.md) page for more information.

#### Contact Info

Joe Mayo started the Generellem project because of his belief in open-source and contributing to the community. Here are a few places you can find Joe at.

* [Joe on X](https://x.com/JoeMayo)
* [Joe on LinkedIn](https://www.linkedin.com/in/joemayo/)
* [Joe on BlueSky](https://bsky.app/profile/joemayo.bsky.social)