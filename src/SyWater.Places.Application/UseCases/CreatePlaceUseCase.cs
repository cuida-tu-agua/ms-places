using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.UseCases;

public sealed class CreatePlaceUseCase(
    IPlaceRepository places,
    IGeographyReader geography,
    TimeProvider clock) : ICreatePlaceUseCase
{
    public async Task<PlaceView> ExecuteAsync(CreatePlaceCommand command, CancellationToken ct)
    {
        var city = await geography.GetCityAsync(command.CityId, ct)
                   ?? throw new CityNotFoundException(command.CityId);

        var isFirstPlace = !await places.OwnerHasActivePlacesAsync(command.OwnerId, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var place = Place.Create(
            command.OwnerId,
            city.CityId,
            command.Name,
            command.Type,
            command.Address,
            currency: city.DefaultCurrency,                              
            measurementUnit: command.MeasurementUnit ?? city.DefaultUnit,
            isDefault: isFirstPlace,
            now);

        await places.AddAsync(place, ct);
        return PlaceView.From(place, city);
    }
}