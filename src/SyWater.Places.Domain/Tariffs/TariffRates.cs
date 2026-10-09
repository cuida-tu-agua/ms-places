namespace SyWater.Places.Domain.Tariffs;

/// <summary>
/// The prices that apply to a place at a given moment. Colombia charges water by ranges of monthly consumption
/// (basic, complementary, luxury) plus a fixed monthly charge; a tariff typed by the user has ONE price for every range.
/// Prices are per cubic meter (m³).
/// </summary>
public sealed record TariffRates(
    decimal FixedCharge,
    decimal BasicPrice,
    decimal ComplementaryPrice,
    decimal LuxuryPrice,
    decimal BasicLimitM3,
    decimal ComplementaryLimitM3)
{
    /// <summary>Colombian reference limits (monthly m³): basic up to 20, complementary up to 40, luxury above.</summary>
    public const decimal DefaultBasicLimitM3 = 20m;
    public const decimal DefaultComplementaryLimitM3 = 40m;

    /// <summary>A tariff the user typed: the same price in every range.</summary>
    public static TariffRates Flat(decimal unitPrice, decimal? fixedCharge) =>
        new(fixedCharge ?? 0m, unitPrice, unitPrice, unitPrice, DefaultBasicLimitM3, DefaultComplementaryLimitM3);
}

/// <summary>One range of the bill with what the period consumed in it.</summary>
public sealed record CostTier(string Name, decimal CubicMeters, decimal PricePerM3, decimal Cost);

public sealed record CostBreakdown(IReadOnlyList<CostTier> Tiers, decimal VolumetricCost, decimal FixedCharge, decimal Total);

/// <summary>
/// HU-056 / HU-069: how much the water of a period costs. Pure rules, no I/O.
///
/// The ranges belong to the MONTH, not to the day: the first 20 m³ of the month are "basic" whichever day they are used.
/// So a day or a week is charged for the slice of the month it occupies: if 12 m³ were used before this period and the
/// period used 15 m³, then 8 m³ fall in the basic range and 7 m³ in the complementary one (the marginal cost of those
/// 15 m³ given what was already used). The fixed charge is monthly: it only joins the total of a MONTH period.
/// </summary>
public static class CostCalculator
{
    public const string BasicName = "Básico";
    public const string ComplementaryName = "Complementario";
    public const string LuxuryName = "Suntuario";

    /// <param name="rates">Prices in force.</param>
    /// <param name="periodM3">Cubic meters of the period being priced.</param>
    /// <param name="usedBeforeM3">Cubic meters of the same month used before the period starts.</param>
    /// <param name="includeFixedCharge">true when the period is a whole month.</param>
    public static CostBreakdown Calculate(TariffRates rates, decimal periodM3, decimal usedBeforeM3, bool includeFixedCharge)
    {
        var period = Math.Max(periodM3, 0m);
        var from = Math.Max(usedBeforeM3, 0m);
        var to = from + period;

        var tiers = new[]
        {
            Tier(BasicName, 0m, rates.BasicLimitM3, rates.BasicPrice, from, to),
            Tier(ComplementaryName, rates.BasicLimitM3, rates.ComplementaryLimitM3, rates.ComplementaryPrice, from, to),
            Tier(LuxuryName, rates.ComplementaryLimitM3, decimal.MaxValue, rates.LuxuryPrice, from, to),
        };

        var volumetric = Round(tiers.Sum(t => t.Cost));
        var fixedCharge = includeFixedCharge ? Round(rates.FixedCharge) : 0m;
        return new CostBreakdown(tiers, volumetric, fixedCharge, volumetric + fixedCharge);
    }

    /// <summary>Cubic meters of [from, to] that fall inside the range [low, high].</summary>
    private static CostTier Tier(string name, decimal low, decimal high, decimal price, decimal from, decimal to)
    {
        var m3 = Math.Max(0m, Math.Min(to, high) - Math.Max(from, low));
        return new CostTier(name, Math.Round(m3, 4), price, Round(m3 * price));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
