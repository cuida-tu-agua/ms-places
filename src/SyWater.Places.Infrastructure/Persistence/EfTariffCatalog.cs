using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Tariffs;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;

/// <summary>Read-only adapter of the tariff catalog (places_rw cannot write it: v1.2 DCL).</summary>
public sealed class EfTariffCatalog(PlacesDbContext db) : ITariffCatalog
{
    public async Task<IReadOnlyList<CatalogEntry>> ListInForceAsync(Guid cityId, DateOnly at, CancellationToken ct)
    {
        // A city has at most 6 strata x a few versions: filter in SQL, pick the newest of each stratum here
        var rows = await db.TariffCatalog.AsNoTracking()
            .Where(c => c.CityId == cityId && c.EffectiveFrom <= at)             // IX_tcat_city_stratum_from
            .ToListAsync(ct);

        return rows
            .GroupBy(c => c.Stratum)
            .Select(g => g.OrderByDescending(c => c.EffectiveFrom).First())
            .Select(ToEntry)
            .ToList();
    }

    public async Task<CatalogEntry?> GetAsync(Guid cityId, int stratum, DateOnly at, CancellationToken ct)
    {
        var rows = await db.TariffCatalog.AsNoTracking()
            .Where(c => c.CityId == cityId && c.Stratum == stratum)
            .OrderByDescending(c => c.EffectiveFrom)
            .ToListAsync(ct);

        // The version in force at that date; for a date before the catalog starts, the oldest version there is
        var row = rows.FirstOrDefault(c => c.EffectiveFrom <= at) ?? rows.LastOrDefault();
        return row is null ? null : ToEntry(row);
    }

    private static CatalogEntry ToEntry(TariffCatalogEntity e) => new(
        e.CityId, e.Stratum, e.Currency,
        new TariffRates(e.FixedCharge, e.BasicPrice, e.ComplementaryPrice, e.LuxuryPrice, e.BasicLimitM3, e.ComplementaryLimitM3),
        e.EffectiveFrom, e.Source);
}
