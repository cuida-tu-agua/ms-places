using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;

namespace SyWater.Places.Application.UseCases;

public sealed class ListPlacesUseCase(
    IPlaceRepository places,
    IGeographyReader geography) : IListPlacesUseCase
{
    public async Task<IReadOnlyList<PlaceView>> ExecuteAsync(Guid ownerId, CancellationToken ct)
    {
        var owned = await places.ListActiveAsync(ownerId, ct);
        if (owned.Count == 0) return [];

        var cityIds = owned.Select(p => p.CityId).Distinct().ToList();
        var cities = await geography.GetCitiesAsync(cityIds, ct);

        return owned
            .OrderByDescending(p => p.IsDefault)   
            .ThenBy(p => p.CreatedAt)             
            .Select(p => PlaceView.From(p, cities.TryGetValue(p.CityId, out var city)
                ? city
                : throw new InvalidOperationException($"City {p.CityId} of place {p.Id} is not in the catalog.")))
            .ToList();
    }
}