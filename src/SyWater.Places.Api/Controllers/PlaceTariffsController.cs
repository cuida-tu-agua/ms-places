using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Tariffs;

namespace SyWater.Places.Api.Controllers;

/// <summary>HU-054 / HU-066 / HU-069 · The tariff (price per m³) of a place of the user, its history and the cost.</summary>
[ApiController]
[Route("api/places/{placeId:guid}")]
public sealed class PlaceTariffsController : ControllerBase
{
    /// <summary>The tariff in force (null while there is none) and the whole history, newest first.</summary>
    [HttpGet("tariff")]
    public async Task<ActionResult<PlaceTariffsView>> Get(
        Guid placeId, [FromServices] IGetPlaceTariffsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    /// <summary>The user types the price per m³ of their bill. It becomes the tariff in force; the previous ones stay in the history.</summary>
    [HttpPut("tariff/manual")]
    public async Task<ActionResult<TariffView>> SetManual(
        Guid placeId, SetManualTariffRequest request, [FromServices] ISetManualTariffUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(new SetManualTariffCommand(
            User.GetUserId(), placeId, request.UnitPricePerM3!.Value, request.FixedMonthlyCharge), ct));

    /// <summary>
    /// The user picks the stratum (1-6) of their home and the preloaded tariff of the city is used (HU-066).
    /// Typing a manual tariff afterwards overrides it; calling this again goes back to the preloaded one.
    /// </summary>
    [HttpPut("tariff/catalog")]
    public async Task<ActionResult<TariffView>> SetCatalog(
        Guid placeId, SetCatalogTariffRequest request, [FromServices] ISetCatalogTariffUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(new SetCatalogTariffCommand(User.GetUserId(), placeId, request.Stratum!.Value), ct));

    /// <summary>
    /// What the water of a period costs (HU-056): period = day | week | month (day by default), tz = IANA time zone.
    /// It is an estimate; with no tariff the money fields are null and the app invites the user to set one.
    /// </summary>
    [HttpGet("cost")]
    public async Task<ActionResult<CostEstimateView>> Cost(
        Guid placeId, [FromQuery] string? period, [FromQuery] string? tz,
        [FromServices] IGetPlaceCostUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, period, tz, ct));
}
