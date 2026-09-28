using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Ports.In;

namespace SyWater.Places.Api.Controllers;

[ApiController]
[Route("api/geography")]
public sealed class GeographyController(IGeographyQueries queries) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryView>>> Countries(CancellationToken ct) =>
        Ok(await queries.ListCountriesAsync(ct));

    [HttpGet("countries/{countryCode}/subdivisions")]
    public async Task<ActionResult<IReadOnlyList<SubdivisionView>>> Subdivisions(string countryCode, CancellationToken ct) =>
        Ok(await queries.ListSubdivisionsAsync(countryCode, ct));

    [HttpGet("subdivisions/{subdivisionId:guid}/cities")]
    public async Task<ActionResult<IReadOnlyList<CityView>>> Cities(Guid subdivisionId, CancellationToken ct) =>
        Ok(await queries.ListCitiesAsync(subdivisionId, ct));
}