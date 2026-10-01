using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Ports.Out;

public interface IPlaceRepository
{
    Task<Place?> GetActiveAsync(Guid placeId, Guid ownerId, CancellationToken ct);

    Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct);

    Task AddAsync(Place place, CancellationToken ct);

    Task UpdateAsync(Place place, CancellationToken ct);
}