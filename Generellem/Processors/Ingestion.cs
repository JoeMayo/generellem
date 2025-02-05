using Generellem.Document.DocumentTypes;
using Generellem.DocumentSource;
using Generellem.Embedding;
using Generellem.Repository;
using Generellem.Services;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Generellem.Processors;

/// <summary>
/// Ingests documents into the system
/// </summary>
public class Ingestion(
    IDocumentHashRepository docHashRep,
    IDocumentSourceFactory docSourceFact,
    IEmbedding embedding,
    ILogger<Ingestion> logger,
    ISearchService searchSvc)
    : IGenerellemIngestion
{
    const string EmptyText = "<empty>";

    /// <summary>
    /// Recursive search of documents from specified document sources
    /// </summary>
    /// <param name="progress">Lets the caller receive progress updates.</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    public virtual async Task IngestDocumentsAsync(IProgress<IngestionProgress> progress,CancellationToken cancelToken)
    {
        int count = 0;

        IEnumerable<IDocumentSource> docSources = docSourceFact.GetDocumentSources();

        progress.Report(new($"Processing document sources..."));

        await searchSvc.CreateIndexAsync(cancelToken);

        foreach (IDocumentSource docSource in docSources)
        {
            progress.Report(new($"Starting on the {docSource.Description} Document Source"));

            List<string> documentReferences = [];

            await foreach (DocumentInfo doc in docSource.GetDocumentsAsync(cancelToken))
            {
                ArgumentNullException.ThrowIfNull(doc);
                ArgumentNullException.ThrowIfNull(doc.DocStream);
                ArgumentNullException.ThrowIfNull(doc.DocType);
                ArgumentException.ThrowIfNullOrEmpty(doc.DocPath);
                ArgumentException.ThrowIfNullOrEmpty(doc.DocumentReference);

                if (doc.DocType.GetType() == typeof(Unknown))
                    continue;

                string fullText;
                try
                {
                    fullText = await doc.DocType.GetTextAsync(doc.DocStream, doc.DocPath);

                    // regardless of whether the file has contents or not, we need to insert it into the DB to track it
                    // setting it to "<empty>" avoids errors when uploading to vector DB
                    if (string.IsNullOrWhiteSpace(fullText))
                        fullText = EmptyText;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(GenerellemLogEvents.DocumentError, ex, "Unable to extract text from stream: {FilePath}", doc.DocPath);
                    continue;
                }

                documentReferences.Add(doc.DocumentReference);

                if (!await ShouldInsertOrUpdateAsync(doc, fullText))
                    continue;

                progress.Report(new($"Ingesting {doc.DocumentReference}", ++count));

                await InsertOrUpdateDocumentAsync(doc, fullText, docSource, progress, cancelToken);

                if (cancelToken.IsCancellationRequested)
                    break;
            }

            await RemoveDeletedDocumentsAsync(docSource.Reference, documentReferences, docSource, cancelToken);

            progress.Report(new($"Completed the {docSource.Description} Document Source"));
            progress.Report(new($""));
        }

        progress.Report(new($"Ingestion is complete. Total documents processed for this job: {count}"));
    }
    
    /// <summary>
    /// Creates an Azure Search index (if it doesn't already exist), uploads document chunks, and indexes the chunks.
    /// </summary>
    /// <param name="doc"><see cref="DocumentInfo"/></param>
    /// <param name="fullText">Plaintext of document.</param>
    /// <param name="docSrc"><see cref="IDocumentSource"/></param>
    /// <param name="progress">Reports progress.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    public virtual async Task InsertOrUpdateDocumentAsync(DocumentInfo doc, string fullText, IDocumentSource docSrc, IProgress<IngestionProgress> progress, CancellationToken cancellationToken)
    {
        if (doc?.DocType is null)
            return;

        List<TextChunk> chunks = await embedding.EmbedAsync(fullText, doc.DocType, doc.DocumentReference, progress, cancellationToken);
        await searchSvc.UploadDocumentsAsync(chunks, cancellationToken);
    }

    /// <summary>
    /// Compares hash of new document vs. hash of previous document to determine if anything changed.
    /// </summary>
    /// <remarks>
    /// This is an optimization to ensure we don't update documents that haven't changed.
    /// If the document doesn't exist in the local DB, it's new and we insert it.
    /// If the hashes are different, we insert the document into the local DB.
    /// </remarks>
    /// <param name="doc"><see cref="DocumentInfo"/> metadata of document.</param>
    /// <param name="fullText">Document text.</param>
    /// <returns>True if the current and previous hashes match.</returns>
    public virtual async Task<bool> ShouldInsertOrUpdateAsync(DocumentInfo doc, string fullText)
    {
        string newHash = ComputeSha256Hash(fullText);

        DocumentHash? document = await docHashRep.GetDocumentHashAsync(doc.DocumentReference);

        if (document?.Hash == null)
            try
            {
                await docHashRep.InsertAsync(new DocumentHash { DocumentReference = doc.DocumentReference, Hash = newHash });
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Unable to insert doc hash - {DocumentReference}, {DocumentHash}, {Exception}",
                    doc.DocumentReference, newHash, ex);
                return false;
            }
        else if (document.Hash != newHash)
            try
            {
                await docHashRep.UpdateAsync(document, newHash);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Unable to update doc hash - {DocumentReference}, {DocumentHash}, {Exception}",
                    doc.DocumentReference, newHash, ex);
                return false;
            }
        else
            return false;

        return true;
    }

    /// <summary>
    /// We're using a SHA256 hash to compare documents.
    /// </summary>
    /// <param name="rawData">Document Text.</param>
    /// <returns>Hash of the document text.</returns>
    public static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));

        StringBuilder sb = new();

        for (int i = 0; i < bytes.Length; i++)
            sb.Append(bytes[i].ToString("x2"));

        return sb.ToString();
    }

    /// <summary>
    /// Deletes doc refs from the vector search DB and local DB that aren't in the documentReferences argument.
    /// </summary>
    /// <remarks>
    /// The assumption here is that for a given document source, we've identified
    /// all of the files that we can process. However, if there's a file in the
    /// index and not in the document source, the file must have been deleted.
    /// </remarks>
    /// <param name="docSourcePrefix">Filters the documentReferences that can be deleted.</param>
    /// <param name="documentReferences">Existing document references.</param>
    /// <param name="docSource">Document Source we're deleting from.</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    public async Task RemoveDeletedDocumentsAsync(string docSourcePrefix, List<string> documentReferences, IDocumentSource docSource, CancellationToken cancellationToken)
    {
        List<TextChunk> chunks = await searchSvc.GetDocumentReferencesAsync(docSourcePrefix, cancellationToken);

        List<string> chunkIdsToDelete = [];
        List<string> chunkDocumentReferencesToDelete = [];

        // delete in index but not in vector DB
        foreach (TextChunk chunk in chunks)
        {
            if (chunk.DocumentReference is null || documentReferences.Contains(chunk.DocumentReference))
                continue;

            if (chunk?.ID is string chunkID)
                chunkIdsToDelete.Add(chunkID);

            if (chunk?.DocumentReference is string chunkDocumentReference)
                chunkDocumentReferencesToDelete.Add(chunkDocumentReference);
        }

        List<string> indexReferences =
            (from chunk in chunks
             select chunk.DocumentReference)
            .Distinct()
            .ToList();

        // delete in hashes but not in Vector DB
        foreach (string docRef in documentReferences)
            if (!indexReferences.Contains(docRef))
                chunkDocumentReferencesToDelete.Add(docRef);

        if (chunkIdsToDelete.Count != 0)
            await RemoveFromSearchAsync(chunkIdsToDelete, docSource, cancellationToken);

        if (chunkDocumentReferencesToDelete.Count != 0)
            await docHashRep.DeleteAsync(chunkDocumentReferencesToDelete);
    }

    /// <summary>
    /// Removes document references from the search DB.
    /// </summary>
    /// <param name="chunkIdsToDelete">IDs of chunks to delete.</param>
    /// <param name="docSrc">Indicates which data source to delete from.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    public virtual async Task RemoveFromSearchAsync(List<string> chunkIdsToDelete, IDocumentSource docSrc, CancellationToken cancellationToken) =>
        await searchSvc.DeleteDocumentReferencesAsync(chunkIdsToDelete, cancellationToken);
}
