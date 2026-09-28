using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

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

    public Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct) =>
        db.Places.AnyAsync(p => p.OwnerId == ownerId && p.DeletedAt == null, ct);   // uses IX_places_owner

    public async Task AddAsync(Place place, CancellationToken ct)
    {
        db.Places.Add(PlaceMapper.ToEntity(place));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Place place, CancellationToken ct)
    {
        // Entities are read with AsNoTracking, so we attach a fresh copy and mark it as modified.
        db.Places.Update(PlaceMapper.ToEntity(place));
        await db.SaveChangesAsync(ct);
    }
}