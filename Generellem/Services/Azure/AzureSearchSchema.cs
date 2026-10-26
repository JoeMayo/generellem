using System.Globalization;

using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

using Generellem.Embedding;

namespace Generellem.Services.Azure;

/// <summary>
/// Azure AI Search fields and filters that match the Qdrant payload.
/// </summary>
public static class AzureSearchSchema
{
    public const string SourceReference = "SourceReference";
    public const string Path = "Path";
    public const string TenantId = "TenantID";
    public const string GroupId = "GroupID";

    /// <summary>
    /// Filterable fields stored alongside the vector, matching Qdrant payload keys.
    /// </summary>
    public static IReadOnlyList<string> FilterFields { get; } =
    [
        SourceReference,
        Path,
        TenantId,
        GroupId
    ];

    public static List<SearchField> CreateFields(int vectorDimensions, string vectorProfileName)
    {
        List<SearchField> fields =
        [
            new SimpleField(nameof(TextChunk.ID), SearchFieldDataType.String)
            {
                IsKey = true,
                IsFilterable = true,
                IsSortable = true,
                IsFacetable = true
            },
            new SearchableField(nameof(TextChunk.DocumentReference))
            {
                IsFilterable = true,
                IsSortable = true,
                IsFacetable = true
            },
            new SearchableField(nameof(TextChunk.Content)) { IsFilterable = true },
            new VectorSearchField(nameof(TextChunk.Embedding), vectorDimensions, vectorProfileName)
        ];

        foreach (string name in FilterFields)
            fields.Add(new SimpleField(name, SearchFieldDataType.String) { IsFilterable = true });

        return fields;
    }

    public static SearchDocument ToDocument(TextChunk chunk, string tenantId, string groupId, string path) =>
        new()
        {
            [nameof(TextChunk.ID)] = chunk.ID ?? string.Empty,
            [nameof(TextChunk.DocumentReference)] = chunk.DocumentReference ?? string.Empty,
            [nameof(TextChunk.Content)] = chunk.Content ?? string.Empty,
            [nameof(TextChunk.Embedding)] = chunk.Embedding.ToArray(),
            [SourceReference] = chunk.SourceReference ?? string.Empty,
            [Path] = path,
            [TenantId] = tenantId,
            [GroupId] = groupId
        };

    public static string SourceReferenceFilter(string sourceReference, string tenantId, string groupId) =>
        And(Eq(SourceReference, sourceReference), Eq(TenantId, tenantId), Eq(GroupId, groupId));

    public static string PathFilter(string path, string tenantId, string groupId) =>
        And(Eq(Path, path), Eq(TenantId, tenantId), Eq(GroupId, groupId));

    public static string TenantFilter(int companyId) =>
        Eq(TenantId, companyId.ToString(CultureInfo.InvariantCulture));

    public static string Eq(string field, string? value) =>
        $"{field} eq '{Escape(value)}'";

    public static string And(params string[] clauses) => string.Join(" and ", clauses);

    public static string Escape(string? value) => (value ?? string.Empty).Replace("'", "''");
}