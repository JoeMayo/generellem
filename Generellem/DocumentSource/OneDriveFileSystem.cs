using Generellem.Document;
using Generellem.Document.DocumentTypes;
using Generellem.Services;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

using System.Net;
using System.Runtime.CompilerServices;

namespace Generellem.DocumentSource;

/// <summary>
/// Supports ingesting documents from a computer file system
/// </summary>
public class OneDriveFileSystem : IMSGraphDocumentSource
{
    /// <summary>
    /// Describes the document source.
    /// </summary>
    public string Description { get; set; } = "OneDrive File System";

    /// <summary>
    /// Used in the vector DB to uniquely identify the document and where it was ingested from.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    readonly IEnumerable<string> DocExtensions = DocumentTypeFactory.GetSupportedDocumentTypes();

    readonly string? baseUrl;
    readonly string? userId;

    readonly IMSGraphClientFactory msGraphFact;
    readonly IPathProvider pathProvider;

    public OneDriveFileSystem(
        string baseUrl,
        string userId,
        IMSGraphClientFactory msGraphFact,
        IPathProviderFactory pathProviderFact)
    {
        this.msGraphFact = msGraphFact;
        this.pathProvider = pathProviderFact.Create(this);

        this.baseUrl = baseUrl;
        this.userId = userId;
    }

    /// <summary>
    /// Based on the configured paths, scan files.
    /// </summary>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>Enumerable of <see cref="DocumentInfo"/>.</returns>
    public async IAsyncEnumerable<DocumentInfo> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));
        ArgumentNullException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));

        GraphServiceClient graphClient = msGraphFact.Create(GKeys.OneDriveScopes, baseUrl, userId, MSGraphTokenType.OneDrive);
        User? user = await graphClient.Me.GetAsync();

        if (user is not null)
            Reference = $"{user.Id}:{nameof(OneDriveFileSystem)}";

        IEnumerable<PathSpec> fileSpecs = await pathProvider.GetPathsAsync($"{nameof(OneDriveFileSystem)}.json");

        if (fileSpecs is null || fileSpecs.Count() == 0)
            yield break;

        foreach (PathSpec spec in fileSpecs)
        {
            if (spec?.Path is not { } path)
                continue;

            string specDescription = spec.Description ?? string.Empty;

            string? driveId = user?.Id;

            if (driveId is null)
                continue;

            List<DriveItem> files = await GetFilesAsync(graphClient, driveId, path);
            
            foreach (var file in files)
            {
                string fileName = file.Name ?? string.Empty;
                string folder = file.ParentReference?.Path?.Substring(file.ParentReference.Path.IndexOf(':') + 1) ?? string.Empty;
                string filePath = Path.Combine(folder, fileName);

                IDocumentType docType = DocumentTypeFactory.Create(fileName);

                Stream? fileStream = await graphClient.Drives[driveId].Items[file.Id].Content.GetAsync();
                yield return new DocumentInfo(Reference, fileStream, docType, filePath, specDescription);

                if (cancelToken.IsCancellationRequested)
                    break;
            }


            if (cancelToken.IsCancellationRequested)
                break;
        }
    }

    /// <summary>
    /// Get files from the OneDrive account.
    /// </summary>
    /// <param name="graphClient"><see cref="GraphServiceClient"/></param>
    /// <param name="driveId">Unique ID for drive to query.</param>
    /// <param name="path">Location on drive to start at.</param>
    /// <returns><see cref="DriveItem"/></returns>
    public async Task<List<DriveItem>> GetFilesAsync(GraphServiceClient graphClient, string driveId, string path)
    {
        DriveItem? driveItem = null;
        try
        {
            driveItem = await graphClient.Drives[driveId].Root
                .ItemWithPath(path)
                .GetAsync();
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            // ignore the error and continue
            // TODO: consider the possibility that we should notify the user that this folder does not exist anymore.
            driveItem = null;
        }

        if (driveItem is null)
            return new();

        return await GetFilesRecursively(graphClient, driveItem, driveId);
    }

    async Task<List<DriveItem>> GetFilesRecursively(GraphServiceClient graphClient, DriveItem driveItem, string driveId)
    {
        List<DriveItem> files = new List<DriveItem>();

        if (driveItem.Folder == null)
        {
            files.Add(driveItem);
        }
        else
        {
            // TODO: use Polly here to back off exponentially on 429 errors
            DriveItemCollectionResponse? children = await graphClient.Drives[driveId].Items[driveItem.Id].Children.GetAsync();

            if (children?.Value is null)
                return files;

            foreach (DriveItem child in children.Value)
                files.AddRange(await GetFilesRecursively(graphClient, child, driveId));
        }

        return files;
    }
}
