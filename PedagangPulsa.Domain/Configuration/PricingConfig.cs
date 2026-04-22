namespace PedagangPulsa.Domain.Configuration;

public class MarginTier
{
    public decimal? MaxCostPrice { get; set; }
    public decimal Margin { get; set; }
}

public class PricingConfig
{
    public List<MarginTier> DefaultMarginTiers { get; set; } = new();

    /// <summary>
    /// Get default margin for a given cost price by matching against tier rules.
    /// Returns 0 if no tiers are configured.
    /// </summary>
    public decimal GetDefaultMargin(decimal costPrice)
    {
        foreach (var tier in DefaultMarginTiers)
        {
            if (tier.MaxCostPrice == null || costPrice <= tier.MaxCostPrice.Value)
                return tier.Margin;
        }

        return 0;
    }
}
