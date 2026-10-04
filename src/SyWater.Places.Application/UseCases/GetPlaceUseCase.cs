using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.UseCases;

public sealed class GetPlaceUseCase(
    IPlaceRepository places,
    IGeographyReader geography) : IGetPlaceUseCase
{
    public async Task<PlaceView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var city = await geography.GetCityAsync(place.CityId, ct)
                   ?? throw new CityNotFoundException(place.CityId);

        return PlaceView.From(place, city);
    }
}