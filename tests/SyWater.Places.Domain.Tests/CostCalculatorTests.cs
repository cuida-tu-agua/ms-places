using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Domain.Tests;

/// <summary>HU-056 / HU-066 / HU-069: cost by ranges of monthly consumption.</summary>
public class CostCalculatorTests
{
    // Basic (<=20 m3) 2000, complementary (<=40 m3) 6000, luxury 9000, fixed charge 10000
    private static readonly TariffRates Ranges = new(10_000m, 2_000m, 6_000m, 9_000m, 20m, 40m);

    [Fact]
    public void A_period_inside_the_basic_range_pays_the_basic_price()
    {
        var cost = CostCalculator.Calculate(Ranges, periodM3: 5m, usedBeforeM3: 0m, includeFixedCharge: false);

        Assert.Equal(10_000m, cost.Total);
        Assert.Equal(5m, cost.Tiers[0].CubicMeters);
        Assert.Equal(0m, cost.Tiers[1].CubicMeters);
        Assert.Equal(0m, cost.Tiers[2].CubicMeters);
    }

    [Fact]
    public void A_period_that_crosses_a_range_is_split_between_both_prices()
    {
        // 12 m3 were used before; this period uses 15: 8 m3 still fit in the basic range, 7 go to complementary
        var cost = CostCalculator.Calculate(Ranges, 15m, 12m, includeFixedCharge: false);

        Assert.Equal(8m, cost.Tiers[0].CubicMeters);
        Assert.Equal(7m, cost.Tiers[1].CubicMeters);
        Assert.Equal(8 * 2_000m + 7 * 6_000m, cost.VolumetricCost);
    }

    [Fact]
    public void A_whole_month_crossing_every_range_pays_the_three_prices()
    {
        var cost = CostCalculator.Calculate(Ranges, 50m, 0m, includeFixedCharge: true);

        Assert.Equal([20m, 20m, 10m], cost.Tiers.Select(t => t.CubicMeters));
        Assert.Equal(20 * 2_000m + 20 * 6_000m + 10 * 9_000m, cost.VolumetricCost);
        Assert.Equal(10_000m, cost.FixedCharge);
        Assert.Equal(cost.VolumetricCost + 10_000m, cost.Total);
    }

    [Fact]
    public void The_fixed_charge_is_monthly_so_a_day_or_a_week_does_not_pay_it()
    {
        var day = CostCalculator.Calculate(Ranges, 1m, 3m, includeFixedCharge: false);

        Assert.Equal(0m, day.FixedCharge);
        Assert.Equal(day.VolumetricCost, day.Total);
    }

    [Fact]
    public void Each_period_is_charged_for_its_own_slice_so_the_days_add_up_to_the_month()
    {
        // 3 days of 10 m3 each = 30 m3 of the month, priced day by day with what was used before
        var days = Enumerable.Range(0, 3)
            .Select(i => CostCalculator.Calculate(Ranges, 10m, i * 10m, includeFixedCharge: false).VolumetricCost)
            .Sum();
        var month = CostCalculator.Calculate(Ranges, 30m, 0m, includeFixedCharge: false).VolumetricCost;

        Assert.Equal(month, days);
        Assert.Equal(20 * 2_000m + 10 * 6_000m, month);
    }

    [Fact]
    public void A_price_typed_by_the_user_is_the_same_in_every_range()
    {
        var flat = TariffRates.Flat(4_000m, 8_000m);

        var cost = CostCalculator.Calculate(flat, 45m, 0m, includeFixedCharge: true);

        Assert.Equal(45 * 4_000m, cost.VolumetricCost);
        Assert.Equal(8_000m, cost.FixedCharge);
        Assert.Equal(45 * 4_000m + 8_000m, cost.Total);
    }

    [Fact]
    public void A_flat_tariff_without_fixed_charge_has_none()
    {
        Assert.Equal(0m, TariffRates.Flat(4_000m, null).FixedCharge);
    }

    [Fact]
    public void Nothing_consumed_costs_nothing_but_the_fixed_charge_of_a_month()
    {
        Assert.Equal(0m, CostCalculator.Calculate(Ranges, 0m, 0m, includeFixedCharge: false).Total);
        Assert.Equal(10_000m, CostCalculator.Calculate(Ranges, 0m, 0m, includeFixedCharge: true).Total);
    }

    [Fact]
    public void Negative_amounts_never_produce_a_negative_cost()
    {
        var cost = CostCalculator.Calculate(Ranges, -5m, -3m, includeFixedCharge: false);

        Assert.Equal(0m, cost.Total);
    }

    [Fact]
    public void Money_is_rounded_to_two_decimals()
    {
        var cost = CostCalculator.Calculate(TariffRates.Flat(1_234.567m, null), 1.2345m, 0m, includeFixedCharge: false);

        Assert.Equal(Math.Round(cost.VolumetricCost, 2), cost.VolumetricCost);
    }
}
