using SyWater.Places.Application.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.Ports.Out;

/// <summary>Guest links, invitations and collaborators of the places.</summary>
public interface ISharingRepository
{
    // ---- Guest link ----
    Task<GuestLink?> GetActiveGuestLinkAsync(Guid placeId, CancellationToken ct);

    Task<GuestLink?> FindActiveGuestLinkByTokenAsync(string token, CancellationToken ct);

    /// <summary>Saves the new link; if <paramref name="replaced"/> is given it is deactivated in the same transaction.</summary>
    Task AddGuestLinkAsync(GuestLink link, GuestLink? replaced, CancellationToken ct);

    Task UpdateGuestLinkAsync(GuestLink link, CancellationToken ct);

    // ---- Invitations ----
    Task<Invitation?> GetInvitationAsync(Guid invitationId, CancellationToken ct);

    /// <summary>The invitation of that place and e-mail whose stored status is PENDING (even if its date already passed).</summary>
    Task<Invitation?> FindPendingInvitationAsync(Guid placeId, string email, CancellationToken ct);

    Task<IReadOnlyList<Invitation>> ListInvitationsAsync(Guid placeId, CancellationToken ct);

    Task<IReadOnlyList<Invitation>> ListPendingInvitationsForEmailAsync(string email, CancellationToken ct);

    Task AddInvitationAsync(Invitation invitation, CancellationToken ct);

    Task UpdateInvitationAsync(Invitation invitation, CancellationToken ct);

    // ---- Collaborators ----
    /// <summary>The row of that user in that place, active or revoked.</summary>
    Task<Member?> FindMemberAsync(Guid placeId, Guid userId, CancellationToken ct);

    Task<Member?> GetActiveMemberAsync(Guid placeId, Guid memberId, CancellationToken ct);

    Task<IReadOnlyList<Member>> ListActiveMembersAsync(Guid placeId, CancellationToken ct);

    Task<IReadOnlyList<Member>> ListActiveMembershipsAsync(Guid userId, CancellationToken ct);

    Task UpdateMemberAsync(Member member, CancellationToken ct);

    /// <summary>The invitation is accepted and the person becomes (or goes back to being) a collaborator, atomically.</summary>
    Task AcceptInvitationAsync(Invitation accepted, Member member, bool memberIsNew, CancellationToken ct);

    /// <summary>Revokes the access and leaves the trace in the activity log, atomically.</summary>
    Task RevokeMemberAsync(Member revoked, PlaceActivity activity, CancellationToken ct);
}
