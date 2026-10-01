using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Ports.Out;

namespace SyWater.Places.Infrastructure.Persistence;

public sealed class EfGeographyReader(PlacesDbContext db) : IGeographyReader
{
    public async Task<CityLocation?> GetCityAsync(Guid cityId, CancellationToken ct)
    {
        var found = await GetCitiesAsync([cityId], ct);
        return found.GetValueOrDefault(cityId);
    }

    public async Task<IReadOnlyDictionary<Guid, CityLocation>> GetCitiesAsync(
        IReadOnlyCollection<Guid> cityIds, CancellationToken ct)
    {
        // SQL: cities JOIN subdivisions JOIN countries WHERE cities.id IN (...)
        var rows = await db.Cities
            .AsNoTracking()
            .Where(c => cityIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                CityName = c.Name,
                c.SubdivisionId,
                SubdivisionName = c.Subdivision.Name,
                CountryCode = c.Subdivision.Country.Code,
                CountryName = c.Subdivision.Country.Name,
                c.Subdivision.Country.DefaultCurrency,
                c.Subdivision.Country.DefaultUnit,
            })
            .ToListAsync(ct);

        // Strings -> enums in memory (after the query), with the same mapper as places.
        return rows.ToDictionary(
            r => r.Id,
            r => new CityLocation(
                r.Id, r.CityName, r.SubdivisionId, r.SubdivisionName, r.CountryCode, r.CountryName,
                PlaceMapper.ToCurrency(r.DefaultCurrency), PlaceMapper.ToUnit(r.DefaultUnit)));
    }

    public async Task<IReadOnlyList<CountryView>> ListCountriesAsync(CancellationToken ct)
    {
        var rows = await db.Countries.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return rows
            .Select(c => new CountryView(c.Code, c.Name,
                PlaceMapper.ToCurrency(c.DefaultCurrency), PlaceMapper.ToUnit(c.DefaultUnit)))
            .ToList();
    }

    public async Task<IReadOnlyList<SubdivisionView>> ListSubdivisionsAsync(string countryCode, CancellationToken ct) =>
        await db.Subdivisions
            .AsNoTracking()
            .Where(s => s.Country.Code == countryCode)
            .OrderBy(s => s.Name)
            .Select(s => new SubdivisionView(s.Id, s.Code, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CityView>> ListCitiesAsync(Guid subdivisionId, CancellationToken ct) =>
        await db.Cities
            .AsNoTracking()
            .Where(c => c.SubdivisionId == subdivisionId)   // uses UQ_cities_subdivision_code
            .OrderBy(c => c.Name)
            .Select(c => new CityView(c.Id, c.Code, c.Name))
            .ToListAsync(ct);
}