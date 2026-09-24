using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;

public class LoggingFunctionFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"[FILTER] Calling function: {context.Function.PluginName}.{context.Function.Name}");
        foreach (var arg in context.Arguments)
        {
            Console.WriteLine($"[FILTER]   Argument: {arg.Key} = {arg.Value}");
        }

        await next(context);

        if (context.Result.GetValue<object>() is IEnumerable<TextSearchResult> results)
        {
            Console.WriteLine($"[FILTER] Result contains {results.Count()} item(s):");
            foreach (var r in results)
            {
                Console.WriteLine($"[FILTER]   Name: {r.Name}");
                Console.WriteLine($"[FILTER]   Value: {r.Value}");
                Console.WriteLine($"[FILTER]   ---");
            }
        }
        else
        {
            Console.WriteLine($"[FILTER]   Result (raw): {context.Result}");
        }
    }
}