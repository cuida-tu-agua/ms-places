using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.UseCases;

public sealed class UpdatePlaceUseCase(
    IPlaceRepository places,
    IGeographyReader geography,
    TimeProvider clock) : IUpdatePlaceUseCase
{
    public async Task<PlaceView> ExecuteAsync(UpdatePlaceCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);

        var newCity = await geography.GetCityAsync(command.CityId, ct)
                      ?? throw new CityNotFoundException(command.CityId);

        var currency = place.Currency;
        if (newCity.CityId != place.CityId)
        {
            var oldCity = await geography.GetCityAsync(place.CityId, ct);
            if (oldCity?.CountryCode != newCity.CountryCode)
                currency = newCity.DefaultCurrency;
        }

        place.Update(command.Name, command.Type, command.Address, newCity.CityId,
                     currency, command.MeasurementUnit, clock.GetUtcNow().UtcDateTime);

        await places.UpdateAsync(place, ct);
        return PlaceView.From(place, newCity);
    }
}