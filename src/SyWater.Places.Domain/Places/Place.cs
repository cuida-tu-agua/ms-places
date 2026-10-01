namespace SyWater.Places.Domain.Places;

public sealed class Place
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 500;

    public Guid Id { get; }
    public Guid OwnerId { get; }
    public Guid CityId { get; private set; }
    public string Name { get; private set; }
    public PlaceType Type { get; private set; }
    public string Address { get; private set; }
    public Currency Currency { get; private set; }
    public MeasurementUnit MeasurementUnit { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private Place(
        Guid id, Guid ownerId, Guid cityId, string name, PlaceType type, string address,
        Currency currency, MeasurementUnit measurementUnit, bool isDefault,
        DateTime createdAt, DateTime updatedAt, DateTime? deletedAt)
    {
        Id = id;
        OwnerId = ownerId;
        CityId = cityId;
        Name = name;
        Type = type;
        Address = address;
        Currency = currency;
        MeasurementUnit = measurementUnit;
        IsDefault = isDefault;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }

    public static Place Create(
        Guid ownerId, Guid cityId, string name, PlaceType type, string address,
        Currency currency, MeasurementUnit measurementUnit, bool isDefault, DateTime now)
    {
        if (ownerId == Guid.Empty) throw new InvalidPlaceException("Owner is required.");
        if (cityId == Guid.Empty) throw new InvalidPlaceException("City is required.");

        return new Place(
            Guid.NewGuid(), ownerId, cityId,
            ValidateText(name, NameMaxLength, "Name"), type,
            ValidateText(address, AddressMaxLength, "Address"),
            currency, measurementUnit, isDefault,
            createdAt: now, updatedAt: now, deletedAt: null);
    }
    
    public static Place Restore(
        Guid id, Guid ownerId, Guid cityId, string name, PlaceType type, string address,
        Currency currency, MeasurementUnit measurementUnit, bool isDefault,
        DateTime createdAt, DateTime updatedAt, DateTime? deletedAt) =>
        new(id, ownerId, cityId, name, type, address, currency, measurementUnit,
            isDefault, createdAt, updatedAt, deletedAt);

    public void Update(
        string name, PlaceType type, string address, Guid cityId,
        Currency currency, MeasurementUnit measurementUnit, DateTime now)
    {
        if (IsDeleted) throw new PlaceNotFoundException(Id);
        if (cityId == Guid.Empty) throw new InvalidPlaceException("City is required.");

        Name = ValidateText(name, NameMaxLength, "Name");
        Type = type;
        Address = ValidateText(address, AddressMaxLength, "Address");
        CityId = cityId;
        Currency = currency;
        MeasurementUnit = measurementUnit;
        UpdatedAt = now;
    }

    public void MarkAsDefault(DateTime now)
    {
        if (IsDeleted) throw new PlaceNotFoundException(Id);
        if (IsDefault) return; // selecting the place that is already selected changes nothing

        IsDefault = true;
        UpdatedAt = now;
    }

    public void Delete(DateTime now)
    {
        if (IsDeleted) throw new PlaceNotFoundException(Id);

        DeletedAt = now;
        IsDefault = false;
        UpdatedAt = now;
    }

    private static string ValidateText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) throw new InvalidPlaceException($"{field} is required.");
        if (trimmed.Length > maxLength)
            throw new InvalidPlaceException($"{field} must have at most {maxLength} characters.");
        return trimmed;
    }
}
