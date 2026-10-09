using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Tips;
using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Api.Controllers;

/// <summary>HU-065 · Water saving tips for the user, with favorites.</summary>
[ApiController]
[Route("api/tips")]
public sealed class TipsController : ControllerBase
{
    /// <summary>
    /// At least 3 different tips of the kind of place: placeId, or category (RESIDENTIAL | COMMERCIAL), or by default the
    /// kind of the selected place of the user. count = 3..10 (5 by default). Only active tips are ever returned.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<TipsSessionView>> Get(
        [FromQuery] Guid? placeId, [FromQuery] TipCategory? category, [FromQuery] int? count,
        [FromServices] IGetTipsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, category, count, ct));

    /// <summary>The active tips the user marked as favorite.</summary>
    [HttpGet("favorites")]
    public async Task<ActionResult<IReadOnlyList<TipView>>> Favorites(
        [FromServices] IManageTipFavoritesUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ListAsync(User.GetUserId(), ct));

    /// <summary>Marks a tip as favorite. Marking it again changes nothing.</summary>
    [HttpPut("{tipId:guid}/favorite")]
    public async Task<ActionResult<TipView>> Mark(
        Guid tipId, [FromServices] IManageTipFavoritesUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.AddAsync(User.GetUserId(), tipId, ct));

    /// <summary>Removes the mark. Removing a tip that is not a favorite changes nothing.</summary>
    [HttpDelete("{tipId:guid}/favorite")]
    public async Task<IActionResult> Unmark(
        Guid tipId, [FromServices] IManageTipFavoritesUseCase useCase, CancellationToken ct)
    {
        await useCase.RemoveAsync(User.GetUserId(), tipId, ct);
        return NoContent();
    }
}

/// <summary>HU-063 / HU-064 · Tip management. Only the ADMIN role of the token gets in.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/tips")]
public sealed class AdminTipsController : ControllerBase
{
    /// <summary>Every tip, newest first. Inactive ones come too unless includeInactive=false.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminTipView>>> List(
        [FromQuery] TipCategory? category, [FromQuery] bool? includeInactive,
        [FromServices] IListAdminTipsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(category, includeInactive ?? true, ct));

    /// <summary>Publishes a tip: it is visible to every user at once.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminTipView>> Create(
        SaveTipRequest request, [FromServices] ICreateTipUseCase useCase, CancellationToken ct)
    {
        var tip = await useCase.ExecuteAsync(
            new SaveTipCommand(User.GetUserId(), null, request.Title!, request.Body!, request.Category!.Value), ct);
        return Created($"api/admin/tips/{tip.Id}", tip);
    }

    /// <summary>Edits a tip. The change applies right away.</summary>
    [HttpPut("{tipId:guid}")]
    public async Task<ActionResult<AdminTipView>> Edit(
        Guid tipId, SaveTipRequest request, [FromServices] IEditTipUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(
            new SaveTipCommand(User.GetUserId(), tipId, request.Title!, request.Body!, request.Category!.Value), ct));

    /// <summary>active=false hides the tip from the users without deleting it; true shows it again.</summary>
    [HttpPut("{tipId:guid}/active")]
    public async Task<ActionResult<AdminTipView>> SetActive(
        Guid tipId, SetTipActiveRequest request, [FromServices] ISetTipActiveUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), tipId, request.Active!.Value, ct));
}
