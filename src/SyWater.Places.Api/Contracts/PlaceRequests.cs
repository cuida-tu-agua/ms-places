using System.ComponentModel.DataAnnotations;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Api.Contracts;

public sealed class CreatePlaceRequest
{
    [Required] public Guid? CityId { get; init; }
    [Required, StringLength(Place.NameMaxLength)] public string? Name { get; init; }
    [Required] public PlaceType? Type { get; init; }
    [Required, StringLength(Place.AddressMaxLength)] public string? Address { get; init; }

    public MeasurementUnit? MeasurementUnit { get; init; }
}

public sealed class UpdatePlaceRequest
{
    [Required] public Guid? CityId { get; init; }
    [Required, StringLength(Place.NameMaxLength)] public string? Name { get; init; }
    [Required] public PlaceType? Type { get; init; }
    [Required, StringLength(Place.AddressMaxLength)] public string? Address { get; init; }
    [Required] public MeasurementUnit? MeasurementUnit { get; init; }
}