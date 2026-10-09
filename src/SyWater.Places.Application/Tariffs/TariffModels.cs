using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Tariffs;

/// <summary>HU-054. The owner comes from the token, never from the body.</summary>
public sealed record SetManualTariffCommand(Guid OwnerId, Guid PlaceId, decimal UnitPricePerM3, decimal? FixedMonthlyCharge);

/// <summary>HU-066: the user picks the stratum (1-6) of their home.</summary>
public sealed record SetCatalogTariffCommand(Guid OwnerId, Guid PlaceId, int Stratum);

/// <summary>HU-066: the prices of one stratum, with the date they were last updated (EffectiveFrom of the row in force).</summary>
public sealed record CatalogRatesView(
    decimal FixedCharge,
    decimal BasicPrice,
    decimal ComplementaryPrice,
    decimal LuxuryPrice,
    decimal BasicLimitM3,
    decimal ComplementaryLimitM3,
    DateOnly UpdatedAt,
    string Source)
{
    public static CatalogRatesView From(CatalogEntry entry) => new(
        entry.Rates.FixedCharge, entry.Rates.BasicPrice, entry.Rates.ComplementaryPrice, entry.Rates.LuxuryPrice,
        entry.Rates.BasicLimitM3, entry.Rates.ComplementaryLimitM3, entry.EffectiveFrom, entry.Source);
}

/// <summary>
/// One entry of the tariff history. A Manual entry carries the prices the user typed; a Catalog entry carries the
/// stratum and the prices of the catalog (Catalog) that applied when the entry was made.
/// </summary>
public sealed record TariffView(
    long Id,
    TariffSource Source,
    decimal? UnitPricePerM3,
    decimal? FixedMonthlyCharge,
    int? Stratum,
    Currency Currency,
    DateTime ValidFrom,
    CatalogRatesView? Catalog = null)
{
    public static TariffView From(PlaceTariff tariff, Currency currency, CatalogEntry? catalog = null) => new(
        tariff.Id, tariff.Source, tariff.UnitPricePerM3, tariff.FixedMonthlyCharge, tariff.Stratum, currency,
        tariff.ValidFrom, catalog is null ? null : CatalogRatesView.From(catalog));
}

/// <summary>
/// Current is null while the place has no tariff: the app then invites the user to set one instead of showing zero (HU-056).
/// History holds every entry, newest first (the first one is Current).
/// </summary>
public sealed record PlaceTariffsView(TariffView? Current, IReadOnlyList<TariffView> History);

public sealed record CatalogStratumView(int Stratum, CatalogRatesView Rates);

/// <summary>HU-066: what the app shows to pick a stratum. Available = false when the city has no preloaded tariffs.</summary>
public sealed record TariffCatalogView(Guid CityId, string CityName, bool Available, IReadOnlyList<CatalogStratumView> Strata);

/// <summary>Which tariff priced the consumption, so the screen can say where the numbers come from (HU-069).</summary>
public sealed record CostTariffInfo(TariffSource Source, int? Stratum, DateTime ValidFrom, DateOnly? CatalogUpdatedAt);

/// <summary>
/// HU-056: the water of a period in money. It is always an ESTIMATE based on the tariff of the place.
/// HasTariff = false means there is no tariff yet: the app invites the user to set one (CatalogAvailable says whether
/// the city has preloaded tariffs to choose from) and the money fields are null instead of zero.
/// </summary>
public sealed record CostEstimateView(
    Guid PlaceId,
    string Period,
    DateTime From,
    DateTime To,
    decimal Liters,
    decimal CubicMeters,
    bool HasTariff,
    bool CatalogAvailable,
    Currency Currency,
    CostTariffInfo? Tariff,
    decimal? VolumetricCost,
    decimal? FixedCharge,
    decimal? Total,
    IReadOnlyList<CostTier> Tiers,
    bool IsEstimate,
    string Disclaimer);
