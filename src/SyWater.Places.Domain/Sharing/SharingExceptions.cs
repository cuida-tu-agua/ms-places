using SyWater.Places.Domain.Common;

namespace SyWater.Places.Domain.Sharing;

/// <summary>A permission that does not exist. HTTP 400.</summary>
public sealed class InvalidPermissionsException(string message)
    : DomainException("permission.invalid", message);

/// <summary>The user is in the place but may not do this. HTTP 403.</summary>
public sealed class PermissionDeniedException(Permission permission)
    : DomainException("permission.denied", $"You do not have the permission {permission.ToKey()} in this place.");

/// <summary>Invalid invitation data (bad e-mail, the owner inviting themselves...). HTTP 400.</summary>
public sealed class InvalidInvitationException(string message)
    : DomainException("invitation.invalid", message);

/// <summary>The invitation does not exist or was not sent to the e-mail of the caller. HTTP 404.</summary>
public sealed class InvitationNotFoundException(Guid invitationId)
    : DomainException("invitation.not_found", $"Invitation {invitationId} was not found.");

/// <summary>INV-I003: that e-mail already has a pending invitation in the place. HTTP 409.</summary>
public sealed class InvitationAlreadyPendingException(string email)
    : DomainException("invitation.already_pending", $"There is already a pending invitation for {email} in this place.");

/// <summary>INV-I002: the person is already a collaborator of the place. HTTP 409.</summary>
public sealed class AlreadyMemberException(string email)
    : DomainException("invitation.already_member", $"{email} already collaborates in this place.");

/// <summary>INV-I005: the invitation expired; the owner must send a new one. HTTP 409.</summary>
public sealed class InvitationExpiredException(Guid invitationId)
    : DomainException("invitation.expired", $"Invitation {invitationId} expired. Ask the owner for a new one.");

/// <summary>The invitation was already answered. HTTP 409.</summary>
public sealed class InvitationNotPendingException(Guid invitationId, InvitationStatus status)
    : DomainException("invitation.not_pending", $"Invitation {invitationId} was already answered ({status}).");

/// <summary>The collaborator does not exist in the place, or was already revoked. HTTP 404.</summary>
public sealed class MemberNotFoundException(Guid memberId)
    : DomainException("member.not_found", $"Collaborator {memberId} was not found in this place.");

/// <summary>The link does not exist, was deactivated or its place was deleted. HTTP 404 (the same for all three).</summary>
public sealed class GuestLinkNotFoundException()
    : DomainException("guest_link.not_found", "This link does not exist or is no longer active.");
