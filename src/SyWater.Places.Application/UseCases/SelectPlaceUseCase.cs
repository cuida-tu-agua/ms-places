using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.UseCases;

public sealed class SelectPlaceUseCase(
    IPlaceRepository places,
    IGeographyReader geography,
    TimeProvider clock) : ISelectPlaceUseCase
{
    public async Task<PlaceView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var city = await geography.GetCityAsync(place.CityId, ct)
                   ?? throw new CityNotFoundException(place.CityId);

        if (!place.IsDefault)
        {
            place.MarkAsDefault(clock.GetUtcNow().UtcDateTime);
            if (!await places.SetDefaultAsync(place, ct))
                throw new PlaceNotFoundException(placeId); // deleted while we were selecting it
        }

        return PlaceView.From(place, city);
    }
}
