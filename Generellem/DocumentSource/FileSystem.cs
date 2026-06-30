using Generellem.Document;
using Generellem.Document.DocumentTypes;

using System.Runtime.CompilerServices;

namespace Generellem.DocumentSource;

/// <summary>
/// Supports ingesting documents from a computer file system
/// </summary>
public class FileSystem : IDocumentSource
{
    /// <summary>
    /// Describes the document source.
    /// </summary>
    public string Description { get; set; } = "File System";

    public string Reference { get; set; } = $"{Environment.MachineName}:{nameof(FileSystem)}";

    readonly IEnumerable<string> DocExtensions = DocumentTypeFactory.GetSupportedDocumentTypes();
    
    readonly IPathProvider pathProvider;

    public FileSystem(IPathProviderFactory pathProviderFact)
    {
        pathProvider = pathProviderFact.Create(this);
    }

    /// <summary>
    /// Based on the config file, scan files.
    /// </summary>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>Enumerable of <see cref="DocumentInfo"/>.</returns>
    public async IAsyncEnumerable<DocumentInfo> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancelToken)
    {
        IEnumerable<PathSpec> fileSpecs = await pathProvider.GetPathsAsync($"{nameof(FileSystem)}.json");

        foreach (PathSpec spec in fileSpecs)
        {
            if (spec?.Path is not string path)
                continue;

            string specDescription = spec.Description ?? string.Empty;

            Queue<string> directories = new();
            directories.Enqueue(path);

            while (directories.Count is not 0)
            {
                string currentDirectory = directories.Dequeue();
                DirectoryInfo directoryInfo = new(currentDirectory);

                foreach (var directory in directoryInfo.GetDirectories())
                    if (!pathProvider.IsPathExcluded(directory.FullName))
                        directories.Enqueue(directory.FullName);

                foreach (FileInfo file in directoryInfo.GetFiles())
                {
                    string filePath = file.FullName;
                    string fileName = Path.GetFileName(filePath);

                    IDocumentType docType = DocumentTypeFactory.Create(fileName);
                    Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    yield return new DocumentInfo(Reference, fileStream, docType, filePath, specDescription);

                    if (cancelToken.IsCancellationRequested)
                        break;
                }

                if (cancelToken.IsCancellationRequested)
                    break;
            }

            if (cancelToken.IsCancellationRequested)
                break;
        }
    }
}
