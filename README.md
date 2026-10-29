![Generellem Logo](images/dp_social.png)

# Generellem

Generellem is an opinionated .NET framework for Retrieval Augmented Generation (RAG). Point it at your documents and ask questions.

Generellem 1.1.0 targets .NET 10 and runs on Linux, macOS, and Windows. The default console profile needs no Azure account, no API keys, and no Docker.

## Try it

Clone the repo and run the console demo. The **InMemory** profile is the default.

```bash
git clone https://github.com/JoeMayo/generellem
cd generellem/GenerellemConsole
dotnet run
```

The first run ingests documents from this repository. `bin`, `obj`, and `.git` are skipped. It also requests the project wiki home page, so a network connection helps. When that request fails, local documents are still ingested.

You will see ingestion progress, then a banner that says `Profile: InMemory`. At the prompt, try:

```text
generellem> How do I contribute to Generellem?
```

The answer comes from files such as `CONTRIBUTING.md`, followed by a Sources list. InMemory and Sqlite do not call a chat model. They return extractive answers from the retrieved text. For a generated answer, use the Ollama profile below.

Type `quit` to exit.

## Profiles

| Profile | Command | What you need |
|---------|---------|----------------|
| **InMemory** (default) | `dotnet run` | Nothing. Extractive answers, kept in memory for the process. |
| **Sqlite** | `dotnet run -- --profile Sqlite` | Nothing. Same extractive answers. Vectors persist in SQLite. |
| **Ollama** | `dotnet run -- --profile Ollama` | [Ollama](https://ollama.com), then `ollama pull llama3.2` and `ollama pull nomic-embed-text`. |
| **Azure** | `dotnet run -- --profile Azure` | Azure OpenAI plus Qdrant. Set `Generellem:UseAzureSearch` to `true` to use Azure AI Search instead. |

Run ingest, questions, or both:

```bash
dotnet run -- --mode ingest
dotnet run -- --mode query
dotnet run -- --mode both
```

`Generellem:RunMode` in config is the same switch. `--mode` overrides it. An unknown mode falls back to `Both`.

Optional Docker services for Qdrant and Ollama:

```bash
docker compose up -d
docker compose exec ollama ollama pull llama3.2
docker compose exec ollama ollama pull nomic-embed-text
```

Do not run a second Ollama on port 11434 at the same time. The comments in `docker-compose.yml` describe that setup.

## Add it to an app

```csharp
services.AddGenerellem(GenerellemProfile.InMemory);
// or GenerellemProfile.Sqlite, Ollama, or Azure
```

Ingestion and query code talk to `ISearchService`. The profile picks the implementation.

## What you get

- **Hybrid search** on every backend. Azure AI Search combines keyword and vector queries. Qdrant adds a `lexical` sparse vector and fuses results with reciprocal rank fusion, and falls back to dense search when sparse search is unavailable. SQLite uses FTS5. InMemory fuses keyword overlap with cosine similarity. A blank query stays vector-only.
- **Reranking** before the prompt is built. `Generellem:RerankMode` is `Lexical` (default), `Llm`, or `None`. Lexical keeps the original order on ties, so the default does not add a model call.
- **Citations.** Retrieved text is wrapped as untrusted context. The answer is followed by a distinct Sources list.
- **Streaming.** `AskStreamAsync` yields tokens, then the citation suffix. Both demos print the stream. `AskAsync` stays non-streaming.
- **Images.** `.jpg`, `.jpeg`, `.png`, `.gif`, and `.bmp` are ingested from embedded text (PNG text chunks and JPEG comments) and the file name. Set `EnableImageVision` and `VisionModel` for an Ollama or Azure vision pass. Vision is off by default.
- **sqlite-vec.** The Sqlite profile uses the bundled sqlite-vec 0.1.9 index when the host can load it (osx-arm64, osx-x64, linux-x64, win-x64). Otherwise search scans with cosine similarity.
- **Releases.** The package version is 1.1.0. Push a `vMAJOR.MINOR.PATCH` tag to pack Generellem and publish a GitHub release.

## Configure

Local options live in `GenerellemConsole/appsettings.json` and `GenerellemConsole/appsettings.Local.json`, under the `Generellem` section.

| Setting | Default | Purpose |
|---------|---------|---------|
| `ChunkSize` | 5000 | Characters per chunk. Text breaks on paragraph, then sentence, then word. |
| `ChunkOverlap` | 100 | Overlap between chunks. Must be less than `ChunkSize`. |
| `TopK` | 3 | How many chunks search returns. |
| `Temperature` | 0 | Chat sampling temperature. |
| `SummarizeUserIntent` | true | Rewrites the question before search. Ollama and Azure use the chat model. InMemory and Sqlite keep the question. Set `false` to skip the step. |
| `EmbeddingBatchSize` | 16 | How many chunks are embedded per ingestion call. |
| `EmbeddingDimensions` | by profile | 64 for InMemory and Sqlite, 768 for Ollama, 1536 for Azure. Override this when your embedding model uses another size. |
| `RunMode` | Both | `Both`, `Ingest`, or `Query`. |
| `RerankMode` | Lexical | `Lexical`, `Llm`, or `None`. |
| `EnableImageVision` | false | When true, `VisionModel` is required. |
| `VisionModel` | empty | Vision model name for Ollama or Azure. |
| `UseAzureSearch` | false | Azure profile only. `false` uses Qdrant. `true` uses Azure AI Search. |
| `OllamaEndpoint` | `http://localhost:11434` | Ollama base URL. |
| `OllamaChatModel` | `llama3.2` | Chat model. |
| `OllamaEmbeddingModel` | `nomic-embed-text` | Embedding model. |
| `OllamaUseSqliteSearch` | false | On the Ollama profile, store vectors in SQLite instead of memory. |

On each ingest, the console copies `FileSystem.json` and `Website.json` into the AppData folder `Generellem/ConsoleDemo`. `FileSystem.json` points at the repository root. Edit those files in `GenerellemConsole` before you run.

### Azure keys

The Azure profile reads user secrets in Debug builds. From `GenerellemConsole`:

```bash
dotnet user-secrets set GenerellemAzOpenAIApiKey "<key>"
dotnet user-secrets set GenerellemAzOpenAIEndpointName "<endpoint>"
dotnet user-secrets set GenerellemAzOpenAIDeploymentName "<chat-deployment>"
dotnet user-secrets set GenerellemAzOpenAIEmbeddingName "<embedding-deployment>"
```

Qdrant, which is the default Azure search backend:

```bash
dotnet user-secrets set GenerellemQdrantEndpoint "<endpoint>"
dotnet user-secrets set GenerellemQdrantApiKey "<key>"
dotnet user-secrets set GenerellemQdrantCollection "<collection>"
```

Azure AI Search, when `UseAzureSearch` is true:

```bash
dotnet user-secrets set GenerellemAzSearchServiceEndpoint "<endpoint>"
dotnet user-secrets set GenerellemAzSearchServiceAdminApiKey "<key>"
dotnet user-secrets set GenerellemAzSearchServiceIndex "<index>"
```

Deployment names are the names you chose in Azure OpenAI. The embedding deployment's dimensions must match `EmbeddingDimensions` (1536 for `text-embedding-ada-002`). For the Qdrant service in `docker-compose.yml`, the endpoint is `http://localhost:6334`. Every Azure key above must be non-blank. InMemory and Sqlite need none. Ollama needs an endpoint and both model names.

If you already have a search index from an older build, ingest again so the new chunk boundaries and index fields are stored.

## Samples

- [GenerellemConsole](GenerellemConsole) is the demo to run first.
- [MSGraphDemo](MSGraphDemo) reads OneDrive through Microsoft Graph. It needs a Graph client. It is not part of the quick start.

The [Documentation](Documentation) folder is sample content the demo can ingest. It is not the getting-started guide.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md). Bugs and ideas go in [Issues](https://github.com/JoeMayo/generellem/issues).

Joe Mayo: [X](https://x.com/JoeMayo) · [LinkedIn](https://www.linkedin.com/in/joemayo/) · [BlueSky](https://bsky.app/profile/joemayo.bsky.social)
