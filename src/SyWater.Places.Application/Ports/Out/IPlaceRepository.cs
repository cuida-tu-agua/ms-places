using SyWater.Places.Application.Places;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Ports.Out;

public interface IPlaceRepository
{
    Task<Place?> GetActiveAsync(Guid placeId, Guid ownerId, CancellationToken ct);

    Task<IReadOnlyList<Place>> ListActiveAsync(Guid ownerId, CancellationToken ct);

    Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct);

    /// <summary>HU-062: places of every owner that are not deleted.</summary>
    Task<int> CountActiveAsync(CancellationToken ct);

    Task AddAsync(Place place, CancellationToken ct);

    Task<bool> UpdateAsync(Place place, CancellationToken ct);

    Task<bool> SetDefaultAsync(Place selected, CancellationToken ct);

    Task<bool> DeleteAsync(Place deleted, Place? newDefault, PlaceActivity activity, CancellationToken ct);
}