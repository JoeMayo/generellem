namespace Generellem.Services;

public class GKeys
{
    //
    // Filters
    //

    public const string GroupID = "GroupID";
    public const string TenantID = "TenantID";
    public const string Path = "Path";

    //
    // Azure Blob Service
    //

    public const string AzBlobConnectionString = "GenerellemAzBlobConnectionString";
    public const string AzBlobContainer = "GenerellemAzBlobContainer";
    public const string AzBlobQueue = "GenerellemAzBlobQueue";

    //
    // Azure OpenAI
    //

    public const string AzOpenAIApiKey = "GenerellemAzOpenAIApiKey";
    public const string AzOpenAIDeploymentName = "GenerellemAzOpenAIDeploymentName";
    public const string AzOpenAIEmbeddingName = "GenerellemAzOpenAIEmbeddingName";
    public const string AzOpenAIEndpointName = "GenerellemAzOpenAIEndpointName";

    //
    // Azure Cognitive Search
    //

    public const string AzSearchServiceAdminApiKey = "GenerellemAzSearchServiceAdminApiKey";
    public const string AzSearchServiceEndpoint = "GenerellemAzSearchServiceEndpoint";
    public const string AzSearchServiceIndex = "GenerellemAzSearchServiceIndex";

    public const string CosmosConnectionString = "GenerellemCosmosConnectionString";
    public const string CosmosContainer = "GenerellemCosmosContainer";
    public const string CosmosDatabase = "GenerellemCosmosDatabase";

    //
    // Cosmos DB
    //

    public const string CosmosConnectionString = "GenerellemCosmosConnectionString";
    public const string CosmosContainer = "GenerellemCosmosContainer";
    public const string CosmosDatabase = "GenerellemCosmosDatabase";

    //
    // MS Graph
    //

    public const string BaseUrl = "BaseUrl";
    public const string MSGraphClientID = "MSGraphClientID";
    public const string MSGraphClientSecret = "MSGraphClientSecret";
    public const string OneDriveUserName = "OneDriveUserName";
    public const string OneDriveScopes = "user.read files.read.all offline_access";
    public const string SharedDocumentsID = "Shared Documents";
    public const string SharePointUserName = "SharePointUserName";
    public const string UserID = "UserID";

    //
    // Qdrant
    //

    public const string QdrantApiKey = "GenerellemQdrantApiKey";
    public const string QdrantCollection = "GenerellemQdrantCollection";
    public const string QdrantEndpoint = "GenerellemQdrantEndpoint";
}
