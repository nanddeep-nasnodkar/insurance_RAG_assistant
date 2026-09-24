using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Azure.AI.OpenAI;
using System.ClientModel;
using CommunityToolkit.VectorData.CosmosNoSql;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;


// --- Configuration ---
IConfigurationRoot config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
string endpoint = config["AZURE_OPENAI_ENDPOINT"];
string deployment = config["AZURE_OPENAI_GPT_NAME"];
string apiKey = config["AZURE_OPENAI_API_KEY"];
string embeddingDeployment = config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"];
string cosmosEndpoint = config["COSMOS_DB_ENDPOINT"];
string cosmosKey = config["COSMOS_DB_KEY"];

// --- Kernel + plugin registration ---
var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
builder.Plugins.AddFromType<InsurancePlugin>("Insurance");
Kernel kernel = builder.Build();

bool enableDebugLogging = false;
if (enableDebugLogging)
{
    kernel.FunctionInvocationFilters.Add(new LoggingFunctionFilter());
}

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
var embeddingClient = azureClient.GetEmbeddingClient(embeddingDeployment);

async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
{
    var result = await embeddingClient.GenerateEmbeddingAsync(text);
    return result.Value.ToFloats();
}

var cosmosClient = new CosmosClient(cosmosEndpoint, cosmosKey, new CosmosClientOptions
{
    UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default
});

var database = cosmosClient.GetDatabase("InsuranceRagDb");
var collection = new CosmosNoSqlCollection<string, PolicyChunk>(database, "PolicyChunks");

await collection.EnsureCollectionExistsAsync();
Console.WriteLine("Collection ready.");

// Set to true only when (re)populating the vector store from the docs/ folder.
// Leave false for normal chat testing — running ingestion repeatedly creates
// duplicate chunks in Cosmos DB, since each run generates new GUIDs as Ids.
bool runIngestion = false;
if (runIngestion)
{
    var policyChunks = new List<PolicyChunk>();
    var docsFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs");

    foreach (var filePath in Directory.GetFiles(docsFolder, "*.txt"))
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fullText = await File.ReadAllTextAsync(filePath);
        var chunks = ChunkText(fullText);

        foreach (var chunk in chunks)
        {
            var embedding = await GetEmbeddingAsync(chunk);

            policyChunks.Add(new PolicyChunk
            {
                Id = Guid.NewGuid().ToString(),
                ChunkText = chunk,
                SourceDocument = fileName,
                Embedding = embedding
            });
        }
    }

    Console.WriteLine($"Generated {policyChunks.Count} chunks with embeddings.");

    foreach (var chunk in policyChunks)
    {
        await collection.UpsertAsync(chunk);
        Console.WriteLine($"Upserted chunk from {chunk.SourceDocument}");
    }

    Console.WriteLine("All chunks upserted.");
}

ITextEmbeddingGenerationService embeddingGenerationService =
    new AzureOpenAITextEmbeddingGenerationService(
        deploymentName: embeddingDeployment,
        endpoint: endpoint,
        apiKey: apiKey);

// 1. Ensure TextSearch knows which property on PolicyChunk contains the vector and text
var textSearch = new VectorStoreTextSearch<PolicyChunk>(
    collection,
    embeddingGenerationService,
    stringMapper: result =>
    {
        if (result is PolicyChunk chunk) return chunk.ChunkText;
        if (result is TextSearchResult textResult) return textResult.Value ?? string.Empty;
        return result?.ToString() ?? string.Empty;
    },
    resultMapper: result =>
    {
        var chunk = (PolicyChunk)result;
        return new TextSearchResult(value: chunk.ChunkText)
        {
            Name = chunk.SourceDocument,
            Value = chunk.ChunkText
        };
    }
);

// Replace the previous searchFunction and searchPlugin code with this:
var policyPlugin = new PolicySearchPlugin(textSearch);

kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(policyPlugin, "PolicySearchPlugin"));



// --- Chat completion service ---
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// --- Chat history with persona ---
ChatHistory history = [];
history.AddSystemMessage("""
    You are an experienced insurance policy assistant working for a home insurance provider.
    You explain coverage terms, deductibles, and claims processes in plain, simple language.
    """);

// --- Execution settings: automatic function calling ---
OpenAIPromptExecutionSettings executionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// --- Main loop ---
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput))
        break;

    history.AddUserMessage(userInput);

    var response = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: executionSettings,
        kernel: kernel
    );

    Console.WriteLine($"AI: {response}\n");
    history.AddAssistantMessage(response.ToString());
}

static List<string> ChunkText(string text)
{
    return text
        .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => p.Length > 0)
        .ToList();
}