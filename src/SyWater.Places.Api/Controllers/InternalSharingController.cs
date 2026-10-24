using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Api.Controllers;

/// <summary>Service-to-service only (X-Internal-Key): the other services ask places about sharing.</summary>
[ApiController]
[AllowAnonymous]
[InternalKey]
[Route("internal")]
public sealed class InternalSharingController : ControllerBase
{
    /// <summary>
    /// Which place a guest token opens (HU-040: ms-consumption shows the water to the guest). 404 = unknown,
    /// deactivated or deleted, and the caller must refuse the guest.
    /// </summary>
    [HttpGet("guest-links/{token}")]
    public async Task<ActionResult<GuestPlaceView>> ResolveGuestLink(
        string token, [FromServices] IResolveGuestLinkUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(token, ct));

    /// <summary>HU-047: who must be told about something of the place: the owner and the collaborators with that permission.</summary>
    [HttpGet("places/{placeId:guid}/recipients")]
    public async Task<ActionResult<IReadOnlyList<PlaceRecipientView>>> Recipients(
        Guid placeId, [FromQuery] string? permission, [FromServices] IGetPlaceRecipientsUseCase useCase, CancellationToken ct)
    {
        if (!PermissionKeys.TryParse(permission, out var parsed))
            throw new InvalidPermissionsException($"'{permission}' is not a valid permission.");
        return Ok(await useCase.ExecuteAsync(placeId, parsed, ct));
    }
}
