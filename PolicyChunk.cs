using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

// PolicyChunk.cs
public class PolicyChunk
{
    [VectorStoreKey]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData(IsFullTextIndexed = true)]
    [JsonPropertyName("chunkText")]
    public string ChunkText { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    [JsonPropertyName("sourceDocument")]
    public string SourceDocument { get; set; } = string.Empty;

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.QuantizedFlat)]
    [JsonPropertyName("embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

