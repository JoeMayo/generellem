
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Generellem.DocumentSource;

/// <summary>
/// Location to get documents for ingestion
/// </summary>
public interface IMSGraphDocumentSource : IDocumentSource
{
    /// <summary>
    /// Get files from the OneDrive account.
    /// </summary>
    /// <param name="graphClient"><see cref="GraphServiceClient"/></param>
    /// <param name="driveId">Unique ID for drive to query.</param>
    /// <param name="path">Location on drive to start at.</param>
    /// <returns><see cref="DriveItem"/></returns>
    IAsyncEnumerable<DriveItem> GetFilesAsync(GraphServiceClient graphClient, string driveId, string path);
}
