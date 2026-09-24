using System.ComponentModel;
using Microsoft.SemanticKernel;

public class InsurancePlugin
{
    [KernelFunction("calculate_premium")]
    [Description("Calculates the annual premium given a home value and a rate percentage")]
    public double CalculatePremium(
        [Description("The insured value of the home in dollars")] double homeValue,
        [Description("The annual rate as a percentage, e.g. 0.5 for 0.5%")] double ratePercent)
    {        
        return homeValue * (ratePercent / 100);
    }

    [KernelFunction("get_coverage_limit")]
    [Description("Looks up the maximum coverage limit for a given policy type")]
    public string GetCoverageLimit(
        [Description("The type of policy, e.g. 'standard', 'premium', or 'basic'")] string policyType)
    {
        return policyType.ToLower() switch
        {
            "basic" => "Coverage limit: $250,000",
            "standard" => "Coverage limit: $500,000",
            "premium" => "Coverage limit: $1,000,000",
            _ => "Unknown policy type. Available types: basic, standard, premium."
        };
    }
}
