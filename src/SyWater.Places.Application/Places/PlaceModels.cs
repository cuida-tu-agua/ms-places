using SyWater.Places.Application.Geography;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Places;

public sealed record CreatePlaceCommand(
    Guid OwnerId,
    Guid CityId,
    string Name,
    PlaceType Type,
    string Address,
    MeasurementUnit? MeasurementUnit);

public sealed record UpdatePlaceCommand(
    Guid OwnerId,
    Guid PlaceId,
    Guid CityId,
    string Name,
    PlaceType Type,
    string Address,
    MeasurementUnit MeasurementUnit);

public sealed record PlaceView(
    Guid Id,
    string Name,
    PlaceType Type,
    string Address,
    Guid CityId,
    string CityName,
    Guid SubdivisionId,
    string SubdivisionName,
    string CountryCode,
    string CountryName,
    Currency Currency,
    MeasurementUnit MeasurementUnit,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static PlaceView From(Place place, CityLocation city) => new(
        place.Id, place.Name, place.Type, place.Address,
        city.CityId, city.CityName, city.SubdivisionId, city.SubdivisionName,
        city.CountryCode, city.CountryName,
        place.Currency, place.MeasurementUnit, place.IsDefault,
        place.CreatedAt, place.UpdatedAt);
}