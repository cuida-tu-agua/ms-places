using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Tests;

// Hand-written fakes for the outbound ports. This is the payoff of hexagonal architecture:
// the use cases are tested without a database, without HTTP and without mocking libraries.

internal sealed class InMemoryPlaceRepository : IPlaceRepository
{
    public List<Place> Items { get; } = [];
    public int UpdateCalls { get; private set; }

    public Task<Place?> GetActiveAsync(Guid placeId, Guid ownerId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == placeId && p.OwnerId == ownerId && !p.IsDeleted));

    public Task<bool> OwnerHasActivePlacesAsync(Guid ownerId, CancellationToken ct) =>
        Task.FromResult(Items.Any(p => p.OwnerId == ownerId && !p.IsDeleted));

    public Task AddAsync(Place place, CancellationToken ct)
    {
        Items.Add(place);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Place place, CancellationToken ct)
    {
        UpdateCalls++; // same instance in memory: nothing else to do
        return Task.CompletedTask;
    }
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