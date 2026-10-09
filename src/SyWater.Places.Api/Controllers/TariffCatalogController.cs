using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Tariffs;

namespace SyWater.Places.Api.Controllers;

/// <summary>HU-066 · The preloaded tariffs of a city, to pick the stratum.</summary>
[ApiController]
[Route("api/tariffs/catalog")]
public sealed class TariffCatalogController : ControllerBase
{
    /// <summary>Strata and prices in force today, with the date each was last updated. Available = false: the city has none.</summary>
    [HttpGet("cities/{cityId:guid}")]
    public async Task<ActionResult<TariffCatalogView>> Get(
        Guid cityId, [FromServices] IGetTariffCatalogUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(cityId, ct));
}
