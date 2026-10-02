using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Tests;

// Hand-written fakes for the outbound ports. This is the payoff of hexagonal architecture:
// the use cases are tested without a database, without HTTP and without mocking libraries.

internal sealed class InMemoryPlaceRepository : IPlaceRepository
{
    public List<Place> Items { get; } = [];
    public List<PlaceActivity> Activity { get; } = [];
    public int UpdateCalls { get; private set; }
    public int SetDefaultCalls { get; private set; }

    public Task<Place?> GetActiveAsync(Guid placeId, Guid ownerId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == placeId && p.OwnerId == ownerId && !p.IsDeleted));

    public Task<IReadOnlyList<Place>> ListActiveAsync(Guid ownerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Place>>(Items.Where(p => p.OwnerId == ownerId && !p.IsDeleted).ToList());

    public Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct) =>
        Task.FromResult(Items.Any(p => p.OwnerId == ownerId && !p.IsDeleted));

    public Task AddAsync(Place place, CancellationToken ct)
    {
        Items.Add(place);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Place place, CancellationToken ct)
    {
        UpdateCalls++; // same instance in memory: nothing else to do
        return Task.FromResult(!place.IsDeleted);
    }

    public Task<bool> SetDefaultAsync(Place selected, CancellationToken ct)
    {
        SetDefaultCalls++;
        UnmarkOtherDefaults(selected);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Place deleted, Place? newDefault, PlaceActivity activity, CancellationToken ct)
    {
        if (newDefault is not null) UnmarkOtherDefaults(newDefault);
        Activity.Add(activity);
        return Task.FromResult(true);
    }

    /// <summary>What the SQL "UPDATE ... SET is_default = 0" does: rebuilds the other places as not default.</summary>
    private void UnmarkOtherDefaults(Place keep)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            var p = Items[i];
            if (p.OwnerId != keep.OwnerId || p.Id == keep.Id || !p.IsDefault) continue;
            Items[i] = Place.Restore(p.Id, p.OwnerId, p.CityId, p.Name, p.Type, p.Address, p.Currency,
                                     p.MeasurementUnit, isDefault: false, p.CreatedAt, keep.UpdatedAt, p.DeletedAt);
        }
    }
}

/// <summary>device-service double: says which places have a device, or pretends to be down.</summary>
internal sealed class FakeDeviceLinkChecker : IDeviceLinkChecker
{
    public HashSet<Guid> PlacesWithDevice { get; } = [];
    public bool IsDown { get; set; }

    public Task<bool> HasActiveDeviceAsync(Guid placeId, CancellationToken ct) =>
        IsDown
            ? throw new ExternalServiceUnavailableException("device-service")
            : Task.FromResult(PlacesWithDevice.Contains(placeId));
}

internal sealed class FakeGeographyReader : IGeographyReader
{
    private static readonly Guid BogotaDc = Guid.NewGuid();
    private static readonly Guid Antioquia = Guid.NewGuid();
    private static readonly Guid Pichincha = Guid.NewGuid();

    public static readonly CityLocation Bogota =
        new(Guid.NewGuid(), "Bogotá, D.C.", BogotaDc, "Bogotá, D.C.", "CO", "Colombia", Currency.Cop, MeasurementUnit.Liters);
    public static readonly CityLocation Medellin =
        new(Guid.NewGuid(), "Medellín", Antioquia, "Antioquia", "CO", "Colombia", Currency.Cop, MeasurementUnit.Liters);
    public static readonly CityLocation Quito =
        new(Guid.NewGuid(), "Quito", Pichincha, "Pichincha", "EC", "Ecuador", Currency.Usd, MeasurementUnit.Liters);

    private readonly Dictionary<Guid, CityLocation> _cities =
        new[] { Bogota, Medellin, Quito }.ToDictionary(c => c.CityId);

    public Task<CityLocation?> GetCityAsync(Guid cityId, CancellationToken ct) =>
        Task.FromResult(_cities.GetValueOrDefault(cityId));

    public Task<IReadOnlyDictionary<Guid, CityLocation>> GetCitiesAsync(IReadOnlyCollection<Guid> cityIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CityLocation>>(
            cityIds.Where(_cities.ContainsKey).ToDictionary(id => id, id => _cities[id]));

    public Task<IReadOnlyList<CountryView>> ListCountriesAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<SubdivisionView>> ListSubdivisionsAsync(string countryCode, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<CityView>> ListCitiesAsync(Guid subdivisionId, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>Controllable clock: each call to Advance moves time forward.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
