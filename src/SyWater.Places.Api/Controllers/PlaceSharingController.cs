using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Sharing;

namespace SyWater.Places.Api.Controllers;

/// <summary>
/// HU-039, HU-042, HU-044, HU-046 · The owner shares a place: guest link, invitations and collaborators.
/// Every route here is for the owner, except <c>access</c> and <c>membership</c>, which any user of the place may call.
/// </summary>
[ApiController]
[Route("api/places/{placeId:guid}")]
public sealed class PlaceSharingController(IConfiguration config) : ControllerBase
{
    private string AppBaseUrl => config["App:BaseUrl"] ?? "";

    /// <summary>
    /// What the caller may do in the place: OWNER, or COLLABORATOR with its permissions. 404 if they have no access at all.
    /// The other services call it with the user's own token to decide a request.
    /// </summary>
    [HttpGet("access")]
    public async Task<ActionResult<PlaceAccessView>> Access(
        Guid placeId, [FromServices] IGetPlaceAccessUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    /// <summary>A collaborator leaves the place by themselves.</summary>
    [HttpDelete("membership")]
    public async Task<IActionResult> Leave(
        Guid placeId, [FromServices] ILeavePlaceUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetUserId(), placeId, ct);
        return NoContent();
    }

    // ── Guest link (HU-039) ──────────────────────────────────────────────

    /// <summary>The active link of the place; 404 if there is none.</summary>
    [HttpGet("guest-link")]
    public async Task<ActionResult<GuestLinkResponse>> GetGuestLink(
        Guid placeId, [FromServices] IGetGuestLinkUseCase useCase, CancellationToken ct)
    {
        var link = await useCase.ExecuteAsync(User.GetUserId(), placeId, ct);
        return link is null ? NotFound() : Ok(GuestLinkResponse.From(link, AppBaseUrl));
    }

    /// <summary>Creates the link, or returns the active one. <c>?regenerate=true</c> replaces it: the old link stops working.</summary>
    [HttpPost("guest-link")]
    public async Task<ActionResult<GuestLinkResponse>> CreateGuestLink(
        Guid placeId, [FromQuery] bool regenerate, [FromServices] ICreateGuestLinkUseCase useCase, CancellationToken ct) =>
        Ok(GuestLinkResponse.From(await useCase.ExecuteAsync(User.GetUserId(), placeId, regenerate, ct), AppBaseUrl));

    /// <summary>Turns the link off: whoever has it loses the access at once.</summary>
    [HttpDelete("guest-link")]
    public async Task<IActionResult> DeactivateGuestLink(
        Guid placeId, [FromServices] IDeactivateGuestLinkUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetUserId(), placeId, ct);
        return NoContent();
    }

    // ── Invitations (HU-042) ─────────────────────────────────────────────

    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<InvitationView>>> ListInvitations(
        Guid placeId, [FromServices] IListInvitationsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    [HttpPost("invitations")]
    public async Task<ActionResult<SentInvitationView>> Invite(
        Guid placeId, InviteCollaboratorRequest request, [FromServices] IInviteCollaboratorUseCase useCase, CancellationToken ct)
    {
        var sent = await useCase.ExecuteAsync(
            new InviteCollaboratorCommand(User.GetUserId(), placeId, request.Email, request.Permissions), ct);
        return StatusCode(StatusCodes.Status201Created, sent);
    }

    /// <summary>The owner withdraws an invitation that was not answered yet.</summary>
    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> CancelInvitation(
        Guid placeId, Guid invitationId, [FromServices] ICancelInvitationUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetUserId(), placeId, invitationId, ct);
        return NoContent();
    }

    // ── Collaborators (HU-044 / HU-046) ──────────────────────────────────

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<MemberView>>> ListMembers(
        Guid placeId, [FromServices] IListMembersUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), placeId, ct));

    /// <summary>The new permissions apply at once.</summary>
    [HttpPut("members/{memberId:guid}/permissions")]
    public async Task<ActionResult<MemberView>> ChangePermissions(
        Guid placeId, Guid memberId, ChangePermissionsRequest request,
        [FromServices] IChangeMemberPermissionsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(
            new ChangePermissionsCommand(User.GetUserId(), placeId, memberId, request.Permissions), ct));

    /// <summary>Revokes the collaborator: the access is cut at once.</summary>
    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid placeId, Guid memberId, [FromServices] IRevokeMemberUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetUserId(), placeId, memberId, ct);
        return NoContent();
    }
}
