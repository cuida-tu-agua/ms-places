using SyWater.Places.Application.Geography;

namespace SyWater.Places.Application.Ports.Out;

public interface IGeographyReader
{
    Task<CityLocation?> GetCityAsync(Guid cityId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, CityLocation>> GetCitiesAsync(
        IReadOnlyCollection<Guid> cityIds, CancellationToken ct);

    Task<IReadOnlyList<CountryView>> ListCountriesAsync(CancellationToken ct);

    Task<IReadOnlyList<SubdivisionView>> ListSubdivisionsAsync(string countryCode, CancellationToken ct);

    Task<IReadOnlyList<CityView>> ListCitiesAsync(Guid subdivisionId, CancellationToken ct);
}