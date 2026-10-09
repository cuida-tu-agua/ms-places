using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Tariffs;

/// <summary>HU-054. The owner comes from the token, never from the body.</summary>
public sealed record SetManualTariffCommand(Guid OwnerId, Guid PlaceId, decimal UnitPricePerM3, decimal? FixedMonthlyCharge);

/// <summary>One entry of the tariff history. For a Catalog entry the prices are resolved from the catalog (HU-066).</summary>
public sealed record TariffView(
    long Id,
    TariffSource Source,
    decimal? UnitPricePerM3,
    decimal? FixedMonthlyCharge,
    int? Stratum,
    Currency Currency,
    DateTime ValidFrom)
{
    public static TariffView From(PlaceTariff tariff, Currency currency) => new(
        tariff.Id, tariff.Source, tariff.UnitPricePerM3, tariff.FixedMonthlyCharge, tariff.Stratum, currency, tariff.ValidFrom);
}

/// <summary>
/// Current is null while the place has no tariff: the app then invites the user to set one instead of showing zero (HU-056).
/// History holds every entry, newest first (the first one is Current).
/// </summary>
public sealed record PlaceTariffsView(TariffView? Current, IReadOnlyList<TariffView> History);
