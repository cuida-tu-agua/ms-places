using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tariffs;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Tests;

/// <summary>In-memory adapter of the tariff catalog, with versions by date like the real table.</summary>
internal sealed class FakeTariffCatalog : ITariffCatalog
{
    public List<CatalogEntry> Rows { get; } = [];

    public void Add(Guid cityId, int stratum, string effectiveFrom, decimal basic, string source = "REFERENCIA") =>
        Rows.Add(new CatalogEntry(cityId, stratum, "COP",
            new TariffRates(10_000m, basic, basic * 3, basic * 4, 20m, 40m), DateOnly.Parse(effectiveFrom), source));

    public Task<IReadOnlyList<CatalogEntry>> ListInForceAsync(Guid cityId, DateOnly at, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogEntry>>(Rows.Where(r => r.CityId == cityId && r.EffectiveFrom <= at)
            .GroupBy(r => r.Stratum).Select(g => g.OrderByDescending(r => r.EffectiveFrom).First()).ToList());

    public Task<CatalogEntry?> GetAsync(Guid cityId, int stratum, DateOnly at, CancellationToken ct)
    {
        var rows = Rows.Where(r => r.CityId == cityId && r.Stratum == stratum).OrderByDescending(r => r.EffectiveFrom).ToList();
        return Task.FromResult(rows.FirstOrDefault(r => r.EffectiveFrom <= at) ?? rows.LastOrDefault());
    }
}

internal sealed class FakeConsumptionReader : IPlaceConsumptionReader
{
    public decimal DayLiters { get; set; }
    public decimal WeekLiters { get; set; }
    public decimal MonthLiters { get; set; }
    public DateTime PeriodStart { get; set; } = new(2026, 10, 9, 5, 0, 0, DateTimeKind.Utc);
    public bool IsDown { get; set; }
    public List<string> Asked { get; } = [];

    public Task<PeriodConsumption> GetAsync(Guid placeId, string period, string? timeZone, CancellationToken ct)
    {
        if (IsDown) throw new ExternalServiceUnavailableException("consumption-service");
        Asked.Add(period);
        var (liters, from) = period switch
        {
            "week" => (WeekLiters, PeriodStart.AddDays(-3)),
            "month" => (MonthLiters, new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc)),
            _ => (DayLiters, PeriodStart),
        };
        return Task.FromResult(new PeriodConsumption(from, from.AddDays(1), liters, liters > 0));
    }
}

/// <summary>HU-066 (catalog by city and stratum), HU-056 (cost) and HU-069 (source and history of the tariff).</summary>
public class TariffCatalogAndCostTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly InMemoryPlaceTariffRepository _tariffs = new();
    private readonly FakeTariffCatalog _catalog = new();
    private readonly FakeConsumptionReader _consumption = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 9, 15, 0, 0, TimeSpan.Zero));
    private readonly Guid _owner = Guid.NewGuid();
    private static readonly Guid Bogota = FakeGeographyReader.Bogota.CityId;

    private async Task<PlaceView> CreatePlace(CityLocation? city = null)
    {
        var view = await new CreatePlaceUseCase(_places, _geography, _clock).ExecuteAsync(
            new CreatePlaceCommand(_owner, (city ?? FakeGeographyReader.Bogota).CityId, "Casa", PlaceType.Residential,
                "Calle 1", null), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return view;
    }

    private SetCatalogTariffUseCase SetCatalog => new(_places, _tariffs, _catalog, _clock);
    private SetManualTariffUseCase SetManual => new(_places, _tariffs, _clock);
    private GetPlaceTariffsUseCase Get => new(_places, _tariffs, _catalog, _clock);
    private GetPlaceCostUseCase Cost => new(_places, _tariffs, _catalog, _consumption, _clock);

    private Task<TariffView> PickStratum(Guid placeId, int stratum) =>
        SetCatalog.ExecuteAsync(new SetCatalogTariffCommand(_owner, placeId, stratum), default);

    private Task<CostEstimateView> CostOf(Guid placeId, string? period = "day") =>
        Cost.ExecuteAsync(_owner, placeId, period, "America/Bogota", default);

    // ── HU-066: catalog of a city ────────────────────────────────────────

    [Fact]
    public async Task The_catalog_of_a_city_lists_its_strata_with_the_date_of_their_last_update()
    {
        _catalog.Add(Bogota, 2, "2026-01-01", 2_000m);
        _catalog.Add(Bogota, 1, "2026-01-01", 1_000m);
        _catalog.Add(Bogota, 1, "2026-07-01", 1_200m, "Resolución nueva");   // an official update: a new version

        var view = await new GetTariffCatalogUseCase(_geography, _catalog, _clock).ExecuteAsync(Bogota, default);

        Assert.True(view.Available);
        Assert.Equal("Bogotá, D.C.", view.CityName);
        Assert.Equal([1, 2], view.Strata.Select(s => s.Stratum));
        Assert.Equal(1_200m, view.Strata[0].Rates.BasicPrice);                        // the newest version of stratum 1
        Assert.Equal(new DateOnly(2026, 7, 1), view.Strata[0].Rates.UpdatedAt);       // "last updated"
        Assert.Equal("Resolución nueva", view.Strata[0].Rates.Source);
    }

    [Fact]
    public async Task A_city_without_preloaded_tariffs_says_it_is_not_available()
    {
        var view = await new GetTariffCatalogUseCase(_geography, _catalog, _clock)
            .ExecuteAsync(FakeGeographyReader.Quito.CityId, default);

        Assert.False(view.Available);
        Assert.Empty(view.Strata);
    }

    [Fact]
    public async Task An_unknown_city_is_rejected()
    {
        await Assert.ThrowsAsync<CityNotFoundException>(() =>
            new GetTariffCatalogUseCase(_geography, _catalog, _clock).ExecuteAsync(Guid.NewGuid(), default));
    }

    // ── HU-066 / HU-069: pick the stratum, source, override ──────────────

    [Fact]
    public async Task Picking_the_stratum_uses_the_prices_of_the_catalog_and_says_so()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();

        var view = await PickStratum(place.Id, 3);

        Assert.Equal(TariffSource.Catalog, view.Source);
        Assert.Equal(3, view.Stratum);
        Assert.Null(view.UnitPricePerM3);                       // the prices are not copied: they come from the catalog
        Assert.Equal(2_000m, view.Catalog!.BasicPrice);
        Assert.Equal(new DateOnly(2026, 1, 1), view.Catalog.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task A_stratum_outside_1_to_6_is_rejected(int stratum)
    {
        var place = await CreatePlace();

        await Assert.ThrowsAsync<InvalidTariffException>(() => PickStratum(place.Id, stratum));
    }

    [Fact]
    public async Task A_stratum_the_city_does_not_have_is_404_and_changes_nothing()
    {
        _catalog.Add(Bogota, 1, "2026-01-01", 1_000m);
        var place = await CreatePlace();

        await Assert.ThrowsAsync<TariffCatalogUnavailableException>(() => PickStratum(place.Id, 4));
        Assert.Empty(_tariffs.Items);
    }

    [Fact]
    public async Task A_city_without_catalog_cannot_pick_a_stratum()
    {
        var quito = await CreatePlace(FakeGeographyReader.Quito);

        await Assert.ThrowsAsync<TariffCatalogUnavailableException>(() => PickStratum(quito.Id, 3));
    }

    [Fact]
    public async Task Picking_the_same_stratum_twice_does_not_duplicate_the_history()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();
        var first = await PickStratum(place.Id, 3);
        _clock.Advance(TimeSpan.FromDays(1));

        var again = await PickStratum(place.Id, 3);

        Assert.Equal(first.Id, again.Id);
        Assert.Single(_tariffs.Items);
    }

    [Fact]
    public async Task Typing_a_manual_tariff_overrides_the_catalog_and_going_back_is_possible()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();
        await PickStratum(place.Id, 3);
        _clock.Advance(TimeSpan.FromDays(1));
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 5_000m, null), default);
        Assert.Equal(TariffSource.Manual, (await Get.ExecuteAsync(_owner, place.Id, default)).Current!.Source);

        _clock.Advance(TimeSpan.FromDays(1));
        await PickStratum(place.Id, 3);                       // back to the preloaded one

        var history = (await Get.ExecuteAsync(_owner, place.Id, default)).History;
        Assert.Equal([TariffSource.Catalog, TariffSource.Manual, TariffSource.Catalog], history.Select(h => h.Source));
    }

    [Fact]
    public async Task The_history_shows_the_catalog_prices_that_applied_at_each_moment()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();
        await PickStratum(place.Id, 3);
        _clock.Advance(TimeSpan.FromDays(1));
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 5_000m, null), default);
        _clock.Advance(TimeSpan.FromDays(1));
        _catalog.Add(Bogota, 3, "2026-10-10", 2_400m);        // official update after the stratum was picked

        var history = (await Get.ExecuteAsync(_owner, place.Id, default)).History;

        // The old catalog entry still shows the prices that applied when it was made, not the update
        Assert.Equal(2_000m, history[1].Catalog!.BasicPrice);
        Assert.Equal(TariffSource.Manual, history[0].Source);
    }

    [Fact]
    public async Task Nobody_can_pick_a_stratum_for_a_place_of_someone_else()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() =>
            SetCatalog.ExecuteAsync(new SetCatalogTariffCommand(Guid.NewGuid(), place.Id, 3), default));
    }

    // ── HU-056: cost ─────────────────────────────────────────────────────

    [Fact]
    public async Task Without_a_tariff_the_cost_is_empty_not_zero_and_the_user_is_invited_to_set_one()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();
        _consumption.DayLiters = 5_000m;

        var cost = await CostOf(place.Id);

        Assert.False(cost.HasTariff);
        Assert.True(cost.CatalogAvailable);                  // the app can offer the stratum picker
        Assert.Null(cost.Total);
        Assert.Null(cost.VolumetricCost);
        Assert.Empty(cost.Tiers);
        Assert.Equal(5m, cost.CubicMeters);                  // the water itself is still reported
    }

    [Fact]
    public async Task A_city_without_catalog_does_not_offer_the_stratum_picker()
    {
        var quito = await CreatePlace(FakeGeographyReader.Quito);

        Assert.False((await CostOf(quito.Id)).CatalogAvailable);
    }

    [Fact]
    public async Task A_day_with_a_manual_tariff_is_the_liters_times_the_price_and_it_is_an_estimate()
    {
        var place = await CreatePlace();
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 4_000m, 9_000m), default);
        _consumption.DayLiters = 2_500m;
        _consumption.MonthLiters = 10_000m;

        var cost = await CostOf(place.Id);

        Assert.True(cost.HasTariff);
        Assert.True(cost.IsEstimate);
        Assert.Contains("estimado", cost.Disclaimer);
        Assert.Equal(2.5m * 4_000m, cost.Total);
        Assert.Equal(0m, cost.FixedCharge);                  // the fixed charge is monthly
        Assert.Equal(TariffSource.Manual, cost.Tariff!.Source);
        Assert.Equal(Currency.Cop, cost.Currency);
    }

    [Fact]
    public async Task A_month_includes_the_fixed_charge()
    {
        var place = await CreatePlace();
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 4_000m, 9_000m), default);
        _consumption.MonthLiters = 10_000m;

        var cost = await CostOf(place.Id, "month");

        Assert.Equal(10m * 4_000m, cost.VolumetricCost);
        Assert.Equal(9_000m, cost.FixedCharge);
        Assert.Equal(49_000m, cost.Total);
        Assert.Equal(["month"], _consumption.Asked);          // the month was asked once, nothing else
    }

    [Fact]
    public async Task With_a_catalog_tariff_a_day_is_priced_by_the_range_it_falls_in()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);       // basic 2000, complementary 6000, luxury 8000
        var place = await CreatePlace();
        await PickStratum(place.Id, 3);
        _consumption.MonthLiters = 27_000m;                  // 27 m3 so far this month
        _consumption.DayLiters = 10_000m;                    // today 10 m3: 17 were used before

        var cost = await CostOf(place.Id);

        Assert.Equal(3m, cost.Tiers[0].CubicMeters);         // 3 m3 still fit in the basic range (17 -> 20)
        Assert.Equal(7m, cost.Tiers[1].CubicMeters);         // 7 m3 go to complementary
        Assert.Equal(3 * 2_000m + 7 * 6_000m, cost.Total);
        Assert.Equal(TariffSource.Catalog, cost.Tariff!.Source);
        Assert.Equal(3, cost.Tariff.Stratum);
        Assert.Equal(new DateOnly(2026, 1, 1), cost.Tariff.CatalogUpdatedAt);
        Assert.Equal(["day", "month"], _consumption.Asked);
    }

    [Fact]
    public async Task An_official_update_does_not_change_the_cost_of_a_period_that_already_started()
    {
        _catalog.Add(Bogota, 3, "2026-01-01", 2_000m);
        var place = await CreatePlace();
        await PickStratum(place.Id, 3);
        _consumption.DayLiters = 5_000m;
        _consumption.MonthLiters = 5_000m;
        var before = (await CostOf(place.Id)).Total;

        _catalog.Add(Bogota, 3, "2026-10-10", 9_999m);       // the utility raises the price AFTER the day being priced
        var after = (await CostOf(place.Id)).Total;

        Assert.Equal(before, after);                         // HU-069: history is not recalculated
    }

    [Fact]
    public async Task A_new_tariff_does_not_reprice_a_period_that_began_before_it_when_there_was_an_older_one()
    {
        var place = await CreatePlace();
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 4_000m, null), default);
        _clock.Advance(TimeSpan.FromDays(10));               // now Oct 19
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 6_000m, null), default);
        _consumption.PeriodStart = new DateTime(2026, 10, 12, 5, 0, 0, DateTimeKind.Utc);   // a day between both tariffs
        _consumption.DayLiters = 1_000m;
        _consumption.MonthLiters = 1_000m;

        var cost = await CostOf(place.Id);

        Assert.Equal(4_000m, cost.Total);                    // the price in force on that day
    }

    [Fact]
    public async Task A_tariff_typed_today_shows_in_the_period_in_progress()
    {
        var place = await CreatePlace();
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 4_000m, null), default);
        _consumption.DayLiters = 1_000m;
        _consumption.MonthLiters = 1_000m;
        Assert.Equal(4_000m, (await CostOf(place.Id)).Total);

        _clock.Advance(TimeSpan.FromHours(1));               // later the same day the user types their real price
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 7_000m, null), default);

        Assert.Equal(7_000m, (await CostOf(place.Id)).Total);   // the day in progress is priced with the new tariff
    }

    [Fact]
    public async Task The_first_tariff_also_prices_the_water_used_before_it_was_typed()
    {
        var place = await CreatePlace();
        _clock.Advance(TimeSpan.FromDays(5));
        await SetManual.ExecuteAsync(new SetManualTariffCommand(_owner, place.Id, 4_000m, null), default);
        _consumption.WeekLiters = 1_000m;                    // the period started before the tariff existed
        _consumption.PeriodStart = new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc);

        var cost = await CostOf(place.Id, "week");

        Assert.True(cost.HasTariff);                         // a tariff typed today must not leave the month without price
    }

    [Fact]
    public async Task No_period_means_day_and_a_wrong_one_is_rejected()
    {
        var place = await CreatePlace();

        Assert.Equal("DAY", (await CostOf(place.Id, null)).Period);
        await Assert.ThrowsAsync<InvalidTariffException>(() => CostOf(place.Id, "year"));
    }

    [Fact]
    public async Task If_consumption_is_down_the_cost_is_unavailable_never_zero()
    {
        var place = await CreatePlace();
        _consumption.IsDown = true;

        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => CostOf(place.Id));
    }

    [Fact]
    public async Task Nobody_can_see_the_cost_of_a_place_of_someone_else()
    {
        var place = await CreatePlace();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() =>
            Cost.ExecuteAsync(Guid.NewGuid(), place.Id, "day", null, default));
    }
}
