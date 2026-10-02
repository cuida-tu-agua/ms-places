using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;

public sealed class EfPlaceRepository(PlacesDbContext db) : IPlaceRepository
{
    public async Task<Place?> GetActiveAsync(Guid placeId, Guid ownerId, CancellationToken ct)
    {
        var entity = await db.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == placeId && p.OwnerId == ownerId && p.DeletedAt == null, ct);

        return entity is null ? null : PlaceMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<Place>> ListActiveAsync(Guid ownerId, CancellationToken ct)
    {
        var entities = await db.Places
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId && p.DeletedAt == null)   // uses IX_places_owner
            .ToListAsync(ct);

        return entities.Select(PlaceMapper.ToDomain).ToList();
    }

    public Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct) =>
        db.Places.AnyAsync(p => p.OwnerId == ownerId && p.DeletedAt == null, ct);   // uses IX_places_owner

    public async Task AddAsync(Place place, CancellationToken ct)
    {
        db.Places.Add(PlaceMapper.ToEntity(place));
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(Place place, CancellationToken ct)
    {
        var type = PlaceMapper.ToDb(place.Type);
        var currency = PlaceMapper.ToDb(place.Currency);
        var unit = PlaceMapper.ToDb(place.MeasurementUnit);

        var rows = await db.Places
            .Where(p => p.Id == place.Id && p.OwnerId == place.OwnerId && p.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Name, place.Name)
                .SetProperty(p => p.PlaceType, type)
                .SetProperty(p => p.Address, place.Address)
                .SetProperty(p => p.CityId, place.CityId)
                .SetProperty(p => p.Currency, currency)
                .SetProperty(p => p.MeasurementUnit, unit)
                .SetProperty(p => p.UpdatedAt, place.UpdatedAt), ct);

        return rows == 1;
    }

    public async Task<bool> SetDefaultAsync(Place selected, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Places
            .Where(p => p.OwnerId == selected.OwnerId && p.IsDefault && p.Id != selected.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDefault, false)
                .SetProperty(p => p.UpdatedAt, selected.UpdatedAt), ct);

        var marked = await db.Places
            .Where(p => p.Id == selected.Id && p.OwnerId == selected.OwnerId && p.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDefault, true)
                .SetProperty(p => p.UpdatedAt, selected.UpdatedAt), ct);

        if (marked == 0) return false;   // not committed: disposing the transaction rolls back step 1

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Place deleted, Place? newDefault, PlaceActivity activity, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Places
            .Where(p => p.Id == deleted.Id && p.OwnerId == deleted.OwnerId && p.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.DeletedAt, deleted.DeletedAt)
                .SetProperty(p => p.IsDefault, false)
                .SetProperty(p => p.UpdatedAt, deleted.UpdatedAt), ct);

        if (rows == 0) return false;

        if (newDefault is not null)
        {
            await db.Places
                .Where(p => p.Id == newDefault.Id && p.DeletedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsDefault, true)
                    .SetProperty(p => p.UpdatedAt, newDefault.UpdatedAt), ct);
        }

        db.ActivityLog.Add(new PlaceActivityEntity
        {
            PlaceId = activity.PlaceId,
            OwnerId = activity.OwnerId,
            Action = activity.Action,
            Metadata = activity.Metadata,
            CreatedAt = activity.OccurredAt,
        });
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return true;
    }
}
