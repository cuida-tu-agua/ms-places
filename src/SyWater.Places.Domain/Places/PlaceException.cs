using SyWater.Places.Domain.Common;

namespace SyWater.Places.Domain.Places;

/// <summary>Invalid data for a place (empty name, address too long, ...). HTTP 400.</summary>
public sealed class InvalidPlaceException(string message)
    : DomainException("place.invalid", message);

/// <summary>The place does not exist, is deleted, or belongs to another owner. HTTP 404.</summary>
public sealed class PlaceNotFoundException(Guid placeId)
    : DomainException("place.not_found", $"Place {placeId} was not found.");

/// <summary>The city is not in the catalog. HTTP 400.</summary>
public sealed class CityNotFoundException(Guid cityId)
    : DomainException("city.not_found", $"City {cityId} was not found.");

/// <summary> a place with a linked device cannot be deleted (unlink it first). HTTP 409.</summary>
public sealed class PlaceHasActiveDeviceException(Guid placeId)
    : DomainException("place.has_active_device",
        $"Place {placeId} has a linked device. Unlink the device before deleting the place.");