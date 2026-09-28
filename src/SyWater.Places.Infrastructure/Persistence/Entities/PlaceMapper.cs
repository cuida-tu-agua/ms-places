using SyWater.Places.Domain.Places;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;


internal static class PlaceMapper
{
    public static Place ToDomain(PlaceEntity e) => Place.Restore(
        e.Id, e.OwnerId, e.CityId, e.Name,
        ToPlaceType(e.PlaceType), e.Address,
        ToCurrency(e.Currency), ToUnit(e.MeasurementUnit),
        e.IsDefault, AsUtc(e.CreatedAt), AsUtc(e.UpdatedAt),
        e.DeletedAt is null ? null : AsUtc(e.DeletedAt.Value));

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static PlaceEntity ToEntity(Place p) => new()
    {
        Id = p.Id,
        OwnerId = p.OwnerId,
        CityId = p.CityId,
        Name = p.Name,
        PlaceType = ToDb(p.Type),
        Address = p.Address,
        Currency = ToDb(p.Currency),
        MeasurementUnit = ToDb(p.MeasurementUnit),
        IsDefault = p.IsDefault,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        DeletedAt = p.DeletedAt,
    };

    public static string ToDb(PlaceType type) => type switch
    {
        PlaceType.Residential => "RESIDENTIAL",
        PlaceType.Commercial => "COMMERCIAL",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static PlaceType ToPlaceType(string value) => value switch
    {
        "RESIDENTIAL" => PlaceType.Residential,
        "COMMERCIAL" => PlaceType.Commercial,
        _ => throw new InvalidOperationException($"Unknown place_type '{value}' in the database."),
    };

    public static string ToDb(Currency currency) => currency switch
    {
        Currency.Cop => "COP",
        Currency.Usd => "USD",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null),
    };

    public static Currency ToCurrency(string value) => value switch
    {
        "COP" => Currency.Cop,
        "USD" => Currency.Usd,
        _ => throw new InvalidOperationException($"Unknown currency '{value}' in the database."),
    };

    public static string ToDb(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Liters => "LITERS",
        MeasurementUnit.CubicMeters => "CUBIC_METERS",
        MeasurementUnit.Gallons => "GALLONS",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    public static MeasurementUnit ToUnit(string value) => value switch
    {
        "LITERS" => MeasurementUnit.Liters,
        "CUBIC_METERS" => MeasurementUnit.CubicMeters,
        "GALLONS" => MeasurementUnit.Gallons,
        _ => throw new InvalidOperationException($"Unknown measurement_unit '{value}' in the database."),
    };
}