using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.Ports.In;

/// <summary>What the user may do in a place: owner, collaborator (with permissions) or nothing (404).</summary>
public interface IPlaceAccessService
{
    /// <exception cref="SyWater.Places.Domain.Places.PlaceNotFoundException">No access at all.</exception>
    Task<PlaceAccess> GetAsync(Guid userId, Guid placeId, CancellationToken ct);

    /// <exception cref="SyWater.Places.Domain.Places.PlaceNotFoundException">No access at all.</exception>
    /// <exception cref="PermissionDeniedException">Access, but without that permission.</exception>
    Task<PlaceAccess> RequireAsync(Guid userId, Guid placeId, Permission permission, CancellationToken ct);
}

/// <summary>For the other services: the role and the permissions of a user in a place.</summary>
public interface IGetPlaceAccessUseCase
{
    Task<PlaceAccessView> ExecuteAsync(Guid userId, Guid placeId, CancellationToken ct);
}

/// <summary>HU-045: the places of other people the user collaborates in.</summary>
public interface IListSharedPlacesUseCase
{
    Task<IReadOnlyList<SharedPlaceView>> ExecuteAsync(Guid userId, CancellationToken ct);
}

/// <summary>HU-047: the users who must be told something about a place (the owner and the collaborators with a permission).</summary>
public interface IGetPlaceRecipientsUseCase
{
    Task<IReadOnlyList<PlaceRecipientView>> ExecuteAsync(Guid placeId, Permission permission, CancellationToken ct);
}

// ---- Guest link ----

/// <summary>HU-039: the owner creates the link of the place (or asks for a new one, which kills the old one).</summary>
public interface ICreateGuestLinkUseCase
{
    Task<GuestLinkView> ExecuteAsync(Guid ownerId, Guid placeId, bool regenerate, CancellationToken ct);
}

public interface IGetGuestLinkUseCase
{
    /// <returns>The active link, or null if the place has none.</returns>
    Task<GuestLinkView?> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

/// <summary>HU-039: the owner turns the link off; whoever has it loses the access at once.</summary>
public interface IDeactivateGuestLinkUseCase
{
    Task ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

/// <summary>HU-040: what a guest sees of the place; also how the other services learn which place a token opens.</summary>
public interface IResolveGuestLinkUseCase
{
    /// <exception cref="GuestLinkNotFoundException">Unknown, deactivated or its place was deleted.</exception>
    Task<GuestPlaceView> ExecuteAsync(string token, CancellationToken ct);
}

// ---- Invitations ----

public interface IInviteCollaboratorUseCase
{
    Task<SentInvitationView> ExecuteAsync(InviteCollaboratorCommand command, CancellationToken ct);
}

public interface IListInvitationsUseCase
{
    Task<IReadOnlyList<InvitationView>> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

/// <summary>The owner withdraws a pending invitation.</summary>
public interface ICancelInvitationUseCase
{
    Task ExecuteAsync(Guid ownerId, Guid placeId, Guid invitationId, CancellationToken ct);
}

/// <summary>HU-043: the invitations waiting for the user (by the e-mail of their account).</summary>
public interface IListMyInvitationsUseCase
{
    Task<IReadOnlyList<InboxInvitationView>> ExecuteAsync(Guid userId, CancellationToken ct);
}

public interface IAnswerInvitationUseCase
{
    Task<SharedPlaceView> AcceptAsync(Guid userId, Guid invitationId, CancellationToken ct);

    Task RejectAsync(Guid userId, Guid invitationId, CancellationToken ct);
}

// ---- Collaborators ----

public interface IListMembersUseCase
{
    Task<IReadOnlyList<MemberView>> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

/// <summary>HU-044.</summary>
public interface IChangeMemberPermissionsUseCase
{
    Task<MemberView> ExecuteAsync(ChangePermissionsCommand command, CancellationToken ct);
}

/// <summary>HU-046: the owner revokes a collaborator.</summary>
public interface IRevokeMemberUseCase
{
    Task ExecuteAsync(Guid ownerId, Guid placeId, Guid memberId, CancellationToken ct);
}

/// <summary>A collaborator leaves a place by themselves.</summary>
public interface ILeavePlaceUseCase
{
    Task ExecuteAsync(Guid userId, Guid placeId, CancellationToken ct);
}
