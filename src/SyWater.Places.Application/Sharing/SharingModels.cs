using SyWater.Places.Application.Places;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.Sharing;

public enum AccessRole
{
    Owner,
    Collaborator,
}

/// <summary>What a user may do in a place: the place plus the permissions they hold in it.</summary>
public sealed record PlaceAccess(Place Place, AccessRole Role, PermissionSet Permissions)
{
    public bool IsOwner => Role == AccessRole.Owner;

    /// <exception cref="PermissionDeniedException">The user is in the place but lacks that permission (HTTP 403).</exception>
    public void Require(Permission permission)
    {
        if (!Permissions.Has(permission)) throw new PermissionDeniedException(permission);
    }
}

/// <summary>What the other services need to decide a request: the role and the permission keys.</summary>
public sealed record PlaceAccessView(Guid PlaceId, string Role, IReadOnlyList<string> Permissions)
{
    public static PlaceAccessView From(PlaceAccess access) => new(
        access.Place.Id,
        access.IsOwner ? "OWNER" : "COLLABORATOR",
        access.Permissions.Keys);
}

/// <summary>HU-045: a place of someone else that the user collaborates in.</summary>
public sealed record SharedPlaceView(PlaceView Place, string Role, IReadOnlyList<string> Permissions, Guid MemberId);

// ---- Guest link (HU-039/040/041) ----

public sealed record GuestLinkView(Guid Id, Guid PlaceId, string Token, DateTime CreatedAt)
{
    public static GuestLinkView From(GuestLink link) => new(link.Id, link.PlaceId, link.Token, link.CreatedAt);
}

/// <summary>What a guest may know about the place: its name, where it is and the unit to show the water in.</summary>
public sealed record GuestPlaceView(Guid PlaceId, string Name, string CityName, MeasurementUnit MeasurementUnit);

// ---- Invitations (HU-042/043) ----

public sealed record InviteCollaboratorCommand(Guid OwnerId, Guid PlaceId, string? Email, IReadOnlyList<string>? Permissions);

public sealed record InvitationView(
    Guid Id,
    string Email,
    IReadOnlyList<string> Permissions,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? RespondedAt)
{
    public static InvitationView From(Invitation invitation, DateTime now) => new(
        invitation.Id, invitation.Email, invitation.Permissions.Keys, invitation.StatusAt(now).ToString().ToUpperInvariant(),
        invitation.ExpiresAt, invitation.CreatedAt, invitation.RespondedAt);
}

/// <summary>The invitation was saved; EmailSent tells whether the e-mail left (the owner can see the invitation either way).</summary>
public sealed record SentInvitationView(InvitationView Invitation, bool EmailSent);

/// <summary>What the invited person sees in their inbox.</summary>
public sealed record InboxInvitationView(
    Guid Id,
    Guid PlaceId,
    string PlaceName,
    string? InvitedByName,
    IReadOnlyList<string> Permissions,
    DateTime ExpiresAt);

/// <summary>What the notifier needs to write the invitation e-mail.</summary>
public sealed record InvitationMail(
    Guid InvitationId, string ToEmail, string PlaceName, string? InvitedByName, IReadOnlyList<string> Permissions, DateTime ExpiresAt);

// ---- Collaborators (HU-043/044/046) ----

public sealed record MemberView(
    Guid Id,
    Guid UserId,
    string? Name,
    string? Email,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt)
{
    public static MemberView From(Member member, UserContact? contact) => new(
        member.Id, member.UserId, contact?.Name, contact?.Email, member.Permissions.Keys, member.CreatedAt);
}

public sealed record ChangePermissionsCommand(Guid OwnerId, Guid PlaceId, Guid MemberId, IReadOnlyList<string>? Permissions);

/// <summary>The person behind a user id, as ms-iam knows them.</summary>
public sealed record UserContact(Guid UserId, string Email, string Name);

/// <summary>HU-047: who must be told about something of a place.</summary>
public sealed record PlaceRecipientView(Guid UserId, string Role);
