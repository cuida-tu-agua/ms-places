using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Tariffs;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;

/// <summary>Outbound adapter for the tariff history. places_rw can only INSERT and SELECT on place_tariffs (v1.2 DCL).</summary>
public sealed class EfPlaceTariffRepository(PlacesDbContext db) : IPlaceTariffRepository
{
    private const string ManualDb = "MANUAL";
    private const string CatalogDb = "CATALOG";

    public async Task<IReadOnlyList<PlaceTariff>> ListAsync(Guid placeId, CancellationToken ct)
    {
        var entities = await db.PlaceTariffs.AsNoTracking()
            .Where(t => t.PlaceId == placeId)                                    // IX_ptar_place_from
            .OrderByDescending(t => t.ValidFrom).ThenByDescending(t => t.Id)
            .ToListAsync(ct);
        return entities.Select(ToDomain).ToList();
    }

    public async Task<PlaceTariff?> GetInForceAsync(Guid placeId, DateTime at, CancellationToken ct)
    {
        var entity = await db.PlaceTariffs.AsNoTracking()
            .Where(t => t.PlaceId == placeId && t.ValidFrom <= at)               // IX_ptar_place_from
            .OrderByDescending(t => t.ValidFrom).ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<PlaceTariff> AddAsync(PlaceTariff tariff, CancellationToken ct)
    {
        var entity = new PlaceTariffEntity
        {
            PlaceId = tariff.PlaceId,
            Source = tariff.Source == TariffSource.Manual ? ManualDb : CatalogDb,
            UnitPrice = tariff.UnitPricePerM3,
            FixedCharge = tariff.FixedMonthlyCharge,
            Stratum = tariff.Stratum is { } s ? (byte)s : null,
            ValidFrom = tariff.ValidFrom,
            CreatedBy = tariff.CreatedBy,
        };
        db.PlaceTariffs.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDomain(entity);
    }

    private static PlaceTariff ToDomain(PlaceTariffEntity e) => PlaceTariff.Restore(
        e.Id, e.PlaceId, e.Source == CatalogDb ? TariffSource.Catalog : TariffSource.Manual,
        e.UnitPrice, e.FixedCharge, e.Stratum, e.ValidFrom, e.CreatedBy);
}
