using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;

namespace SyWater.Places.Application.UseCases;

public sealed class GeographyQueries(IGeographyReader geography) : IGeographyQueries
{
    public Task<IReadOnlyList<CountryView>> ListCountriesAsync(CancellationToken ct) =>
        geography.ListCountriesAsync(ct);

    public Task<IReadOnlyList<SubdivisionView>> ListSubdivisionsAsync(string countryCode, CancellationToken ct) =>
        geography.ListSubdivisionsAsync(countryCode.Trim().ToUpperInvariant(), ct);

    public Task<IReadOnlyList<CityView>> ListCitiesAsync(Guid subdivisionId, CancellationToken ct) =>
        geography.ListCitiesAsync(subdivisionId, ct);
}