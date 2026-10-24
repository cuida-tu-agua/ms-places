using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Sharing;

namespace SyWater.Places.Api.Controllers;

/// <summary>HU-043 / HU-045 · What the invited person does: see the places shared with them and answer invitations.</summary>
[ApiController]
[Route("api")]
public sealed class CollaborationController : ControllerBase
{
    /// <summary>The places of other people the caller collaborates in, with their permissions in each one.</summary>
    [HttpGet("shared-places")]
    public async Task<ActionResult<IReadOnlyList<SharedPlaceView>>> SharedPlaces(
        [FromServices] IListSharedPlacesUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), ct));

    /// <summary>The invitations waiting for the caller (sent to the e-mail of their account).</summary>
    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<InboxInvitationView>>> Inbox(
        [FromServices] IListMyInvitationsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(User.GetUserId(), ct));

    /// <summary>Accepting makes the caller a collaborator with the permissions of the invitation.</summary>
    [HttpPost("invitations/{invitationId:guid}/accept")]
    public async Task<ActionResult<SharedPlaceView>> Accept(
        Guid invitationId, [FromServices] IAnswerInvitationUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.AcceptAsync(User.GetUserId(), invitationId, ct));

    [HttpPost("invitations/{invitationId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid invitationId, [FromServices] IAnswerInvitationUseCase useCase, CancellationToken ct)
    {
        await useCase.RejectAsync(User.GetUserId(), invitationId, ct);
        return NoContent();
    }
}
