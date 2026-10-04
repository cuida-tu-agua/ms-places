using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;

namespace SyWater.Places.Api.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlaceView>>> List(
        [FromServices] IListPlacesUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), ct));

    [HttpGet("{placeId:guid}")]
    public async Task<ActionResult<PlaceView>> Get(
        Guid placeId, [FromServices] IGetPlaceUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    [HttpPost]
    public async Task<ActionResult<PlaceView>> Create(
        CreatePlaceRequest request, [FromServices] ICreatePlaceUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(new CreatePlaceCommand(
            User.GetUserId(),
            request.CityId!.Value,
            request.Name!,
            request.Type!.Value,
            request.Address!,
            request.MeasurementUnit), ct);

        return CreatedAtAction(nameof(Get), new { placeId = view.Id }, view);
    }

    [HttpPut("{placeId:guid}")]
    public async Task<ActionResult<PlaceView>> Update(
        Guid placeId, UpdatePlaceRequest request, [FromServices] IUpdatePlaceUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(new UpdatePlaceCommand(
            User.GetUserId(),
            placeId,
            request.CityId!.Value,
            request.Name!,
            request.Type!.Value,
            request.Address!,
            request.MeasurementUnit!.Value), ct));

    [HttpPut("{placeId:guid}/default")]
    public async Task<ActionResult<PlaceView>> Select(
        Guid placeId, [FromServices] ISelectPlaceUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    [HttpDelete("{placeId:guid}")]
    public async Task<IActionResult> Delete(
        Guid placeId, [FromServices] IDeletePlaceUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetUserId(), placeId, ct);
        return NoContent();
    }
}
