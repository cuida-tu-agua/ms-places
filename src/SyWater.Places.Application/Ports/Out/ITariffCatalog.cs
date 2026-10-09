using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Ports.Out;

/// <summary>One row of the tariff catalog: the prices of a stratum of a city, valid from a date (HU-066).</summary>
public sealed record CatalogEntry(
    Guid CityId,
    int Stratum,
    string Currency,
    TariffRates Rates,
    DateOnly EffectiveFrom,
    string Source);

/// <summary>Outbound port: the preloaded tariffs by city and stratum. Read-only: migrations load it.</summary>
public interface ITariffCatalog
{
    /// <summary>For each stratum of the city, the row in force at <paramref name="at"/> (the newest with EffectiveFrom &lt;= at), by stratum.</summary>
    Task<IReadOnlyList<CatalogEntry>> ListInForceAsync(Guid cityId, DateOnly at, CancellationToken ct);

    /// <summary>
    /// The row of the stratum in force at <paramref name="at"/>. If the city's catalog starts later than that date, the
    /// oldest row (the best data there is); null when the city has no catalog for that stratum.
    /// </summary>
    Task<CatalogEntry?> GetAsync(Guid cityId, int stratum, DateOnly at, CancellationToken ct);
}
