using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tariffs;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.UseCases;

internal static class TariffRules
{
    public static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(utc);

    /// <summary>
    /// The entry that prices a moment: the newest with ValidFrom &lt;= that moment. If the user set their first tariff AFTER
    /// that moment, the first tariff covers it too (otherwise a tariff typed today would leave the month in progress
    /// without price). Entries come newest first.
    /// </summary>
    public static PlaceTariff? PickFor(IReadOnlyList<PlaceTariff> newestFirst, DateTime at) =>
        newestFirst.FirstOrDefault(t => t.ValidFrom <= at) ?? newestFirst.LastOrDefault();
}

public sealed class GetPlaceTariffsUseCase(
    IPlaceRepository places, IPlaceTariffRepository tariffs, ITariffCatalog catalog, TimeProvider clock)
    : IGetPlaceTariffsUseCase
{
    public async Task<PlaceTariffsView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var now = clock.GetUtcNow().UtcDateTime;
        var history = new List<TariffView>();
        foreach (var entry in await tariffs.ListAsync(place.Id, ct))
        {
            // The current entry shows the catalog of today; an old one the catalog that applied when it was made
            var at = history.Count == 0 ? now : entry.ValidFrom;
            CatalogEntry? prices = entry.Source == TariffSource.Catalog
                ? await catalog.GetAsync(place.CityId, entry.Stratum!.Value, TariffRules.DayOf(at), ct)
                : null;
            history.Add(TariffView.From(entry, place.Currency, prices));
        }

        return new PlaceTariffsView(history.FirstOrDefault(), history);
    }
}

public sealed class SetManualTariffUseCase(IPlaceRepository places, IPlaceTariffRepository tariffs, TimeProvider clock)
    : ISetManualTariffUseCase
{
    public async Task<TariffView> ExecuteAsync(SetManualTariffCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);

        var now = clock.GetUtcNow().UtcDateTime;
        var tariff = PlaceTariff.Manual(place.Id, command.UnitPricePerM3, command.FixedMonthlyCharge, command.OwnerId, now);

        // Same prices as the tariff in force: do not fill the history with duplicates
        var current = await tariffs.GetInForceAsync(place.Id, now, ct);
        if (current is not null && current.HasSameManualPrices(command.UnitPricePerM3, command.FixedMonthlyCharge))
            return TariffView.From(current, place.Currency);

        var saved = await tariffs.AddAsync(tariff, ct);
        return TariffView.From(saved, place.Currency);
    }
}

/// <summary>HU-066 / HU-069: use the preloaded tariff of the city for the stratum of the user.</summary>
public sealed class SetCatalogTariffUseCase(
    IPlaceRepository places, IPlaceTariffRepository tariffs, ITariffCatalog catalog, TimeProvider clock)
    : ISetCatalogTariffUseCase
{
    public async Task<TariffView> ExecuteAsync(SetCatalogTariffCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);

        var now = clock.GetUtcNow().UtcDateTime;
        var tariff = PlaceTariff.Catalog(place.Id, command.Stratum, command.OwnerId, now);   // validates 1-6

        var prices = await catalog.GetAsync(place.CityId, command.Stratum, TariffRules.DayOf(now), ct)
                     ?? throw new TariffCatalogUnavailableException(place.CityId, command.Stratum);

        var current = await tariffs.GetInForceAsync(place.Id, now, ct);
        if (current is not null && current.HasSameStratum(command.Stratum))
            return TariffView.From(current, place.Currency, prices);

        var saved = await tariffs.AddAsync(tariff, ct);
        return TariffView.From(saved, place.Currency, prices);
    }
}

/// <summary>HU-066: strata and prices of a city, as of today, with the date of their last update.</summary>
public sealed class GetTariffCatalogUseCase(IGeographyReader geography, ITariffCatalog catalog, TimeProvider clock)
    : IGetTariffCatalogUseCase
{
    public async Task<TariffCatalogView> ExecuteAsync(Guid cityId, CancellationToken ct)
    {
        var city = await geography.GetCityAsync(cityId, ct) ?? throw new CityNotFoundException(cityId);
        var rows = await catalog.ListInForceAsync(cityId, TariffRules.DayOf(clock.GetUtcNow().UtcDateTime), ct);

        var strata = rows
            .OrderBy(r => r.Stratum)
            .Select(r => new CatalogStratumView(r.Stratum, CatalogRatesView.From(r)))
            .ToList();
        return new TariffCatalogView(cityId, city.CityName, strata.Count > 0, strata);
    }
}

/// <summary>HU-056 / HU-069: the money of a period. Always labelled as an estimate.</summary>
public sealed class GetPlaceCostUseCase(
    IPlaceRepository places,
    IPlaceTariffRepository tariffs,
    ITariffCatalog catalog,
    IPlaceConsumptionReader consumption,
    TimeProvider clock) : IGetPlaceCostUseCase
{
    public const string Disclaimer =
        "Valor estimado con la tarifa que configuraste para este lugar. El valor de tu factura puede variar.";

    private static readonly HashSet<string> Periods = ["day", "week", "month"];

    public async Task<CostEstimateView> ExecuteAsync(
        Guid ownerId, Guid placeId, string? period, string? timeZone, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var kind = string.IsNullOrWhiteSpace(period) ? "day" : period.Trim().ToLowerInvariant();
        if (!Periods.Contains(kind))
            throw new InvalidTariffException("The period must be day, week or month.");

        var used = await consumption.GetAsync(place.Id, kind, timeZone, ct);
        var cubicMeters = Math.Round(used.TotalLiters / 1000m, 4);

        // The tariff that applies is the one in force when the period ENDS, or today if it is still running: a price the
        // user types today must show in the month in progress, and a period that already closed is never touched by a
        // later change (it ended before the change was made).
        var now = clock.GetUtcNow().UtcDateTime;
        var pricedAt = used.To < now ? used.To : now;

        var history = await tariffs.ListAsync(place.Id, ct);
        var tariff = TariffRules.PickFor(history, pricedAt);
        if (tariff is null)
        {
            var offered = await catalog.ListInForceAsync(place.CityId, TariffRules.DayOf(now), ct);
            return Estimate(place, kind, used, cubicMeters, hasTariff: false, catalogAvailable: offered.Count > 0);
        }

        TariffRates rates;
        DateOnly? catalogUpdated = null;
        if (tariff.Source == TariffSource.Manual)
        {
            rates = TariffRates.Flat(tariff.UnitPricePerM3!.Value, tariff.FixedMonthlyCharge);
        }
        else
        {
            // The prices that were official on that day: an update dated later never changes this number
            var row = await catalog.GetAsync(place.CityId, tariff.Stratum!.Value, TariffRules.DayOf(pricedAt), ct)
                      ?? throw new TariffCatalogUnavailableException(place.CityId, tariff.Stratum);
            rates = row.Rates;
            catalogUpdated = row.EffectiveFrom;
        }

        // The ranges belong to the month: a day or a week is priced for the slice of the month it occupies
        decimal usedBeforeM3 = 0m;
        if (kind != "month")
        {
            var monthToDate = await consumption.GetAsync(place.Id, "month", timeZone, ct);
            usedBeforeM3 = Math.Max(Math.Round(monthToDate.TotalLiters / 1000m, 4) - cubicMeters, 0m);
        }

        var cost = CostCalculator.Calculate(rates, cubicMeters, usedBeforeM3, includeFixedCharge: kind == "month");
        var info = new CostTariffInfo(tariff.Source, tariff.Stratum, tariff.ValidFrom, catalogUpdated);
        return Estimate(place, kind, used, cubicMeters, hasTariff: true, catalogAvailable: true, info, cost);
    }

    private static CostEstimateView Estimate(
        Place place, string kind, PeriodConsumption used, decimal cubicMeters, bool hasTariff, bool catalogAvailable,
        CostTariffInfo? info = null, CostBreakdown? cost = null) => new(
        place.Id, kind.ToUpperInvariant(), used.From, used.To, used.TotalLiters, cubicMeters,
        hasTariff, catalogAvailable, place.Currency, info,
        cost?.VolumetricCost, cost?.FixedCharge, cost?.Total, cost?.Tiers ?? [],
        IsEstimate: true, Disclaimer);
}
