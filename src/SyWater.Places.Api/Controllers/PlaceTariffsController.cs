using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Tariffs;

namespace SyWater.Places.Api.Controllers;

/// <summary>HU-054 · The tariff (price per m³) of a place of the user and its history.</summary>
[ApiController]
[Route("api/places/{placeId:guid}/tariff")]
public sealed class PlaceTariffsController : ControllerBase
{
    /// <summary>The tariff in force (null while there is none) and the whole history, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PlaceTariffsView>> Get(
        Guid placeId, [FromServices] IGetPlaceTariffsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    /// <summary>The user types the price per m³ of their bill. It becomes the tariff in force; the previous ones stay in the history.</summary>
    [HttpPut("manual")]
    public async Task<ActionResult<TariffView>> SetManual(
        Guid placeId, SetManualTariffRequest request, [FromServices] ISetManualTariffUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(new SetManualTariffCommand(
            User.GetUserId(), placeId, request.UnitPricePerM3!.Value, request.FixedMonthlyCharge), ct));
}
