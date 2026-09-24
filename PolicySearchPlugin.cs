using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;

public class PolicySearchPlugin
{
    private readonly VectorStoreTextSearch<PolicyChunk> _textSearch;

    public PolicySearchPlugin(VectorStoreTextSearch<PolicyChunk> textSearch)
    {
        _textSearch = textSearch;
    }

    [KernelFunction("SearchPolicyDocuments")]
    [Description("Searches insurance policy documents for specific coverage terms, repair limits, deductibles, and claims procedures.")]
    public async Task<List<string>> SearchPolicyDocumentsAsync(
        [Description("The search query or key terms to look up in the policy documents.")] string query)
    {
        var searchResults = await _textSearch.GetTextSearchResultsAsync(query, new TextSearchOptions { Top = 3 });
        var results = new List<string>();

        await foreach (var item in searchResults.Results)
        {
            results.Add($"Source: {item.Name}\nContent: {item.Value}");
        }

        return results;
    }
}