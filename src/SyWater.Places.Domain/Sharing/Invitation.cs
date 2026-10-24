using System.Text.RegularExpressions;

namespace SyWater.Places.Domain.Sharing;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired,
}

/// <summary>
/// The owner's request for a person (by e-mail) to collaborate in a place (HU-042). It lives 7 days. The person
/// answers it with an account whose verified e-mail is the invited one (HU-043), so the invitation itself carries no
/// secret: knowing its id is not enough to use it.
/// </summary>
public sealed partial class Invitation
{
    public const int EmailMaxLength = 320;
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    public Guid Id { get; }
    public Guid PlaceId { get; }
    public Guid InvitedBy { get; }
    public string Email { get; }
    public PermissionSet Permissions { get; }
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    private Invitation(Guid id, Guid placeId, Guid invitedBy, string email, PermissionSet permissions,
        InvitationStatus status, DateTime expiresAt, DateTime createdAt, DateTime updatedAt, DateTime? respondedAt)
    {
        Id = id;
        PlaceId = placeId;
        InvitedBy = invitedBy;
        Email = email;
        Permissions = permissions;
        Status = status;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RespondedAt = respondedAt;
    }

    public static Invitation Create(Guid placeId, Guid invitedBy, string? email, PermissionSet permissions, DateTime now)
    {
        if (placeId == Guid.Empty) throw new InvalidInvitationException("Place is required.");
        if (invitedBy == Guid.Empty) throw new InvalidInvitationException("The person who invites is required.");
        return new Invitation(Guid.NewGuid(), placeId, invitedBy, NormalizeEmail(email), permissions,
            InvitationStatus.Pending, now + Validity, now, now, null);
    }

    public static Invitation Restore(Guid id, Guid placeId, Guid invitedBy, string email, PermissionSet permissions,
        InvitationStatus status, DateTime expiresAt, DateTime createdAt, DateTime updatedAt, DateTime? respondedAt) =>
        new(id, placeId, invitedBy, email, permissions, status, expiresAt, createdAt, updatedAt, respondedAt);

    /// <summary>Trimmed and lower-case, so "Ana@Mail.com " and "ana@mail.com" are the same person. Invalid = HTTP 400.</summary>
    public static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Length == 0) throw new InvalidInvitationException("The e-mail is required.");
        if (normalized.Length > EmailMaxLength)
            throw new InvalidInvitationException($"The e-mail must have at most {EmailMaxLength} characters.");
        if (!EmailShape().IsMatch(normalized)) throw new InvalidInvitationException("The e-mail is not valid.");
        return normalized;
    }

    /// <summary>What the owner sees: a pending invitation past its date is EXPIRED.</summary>
    public InvitationStatus StatusAt(DateTime now) =>
        Status == InvitationStatus.Pending && now >= ExpiresAt ? InvitationStatus.Expired : Status;

    /// <summary>True if the invitation was sent to that e-mail (case does not matter).</summary>
    public bool IsFor(string? email) =>
        !string.IsNullOrWhiteSpace(email) && string.Equals(Email, email.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>HU-043. Expired = 409 invitation.expired; already answered = 409 invitation.not_pending.</summary>
    public void Accept(DateTime now)
    {
        RequireOpenAt(now);
        Status = InvitationStatus.Accepted;
        RespondedAt = now;
        UpdatedAt = now;
    }

    /// <summary>HU-043: "puedo rechazar la invitación".</summary>
    public void Reject(DateTime now)
    {
        RequireOpenAt(now);
        Status = InvitationStatus.Rejected;
        RespondedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Closes a pending invitation that is past its date, so the owner can invite that e-mail again.</summary>
    public void Expire(DateTime now)
    {
        if (StatusAt(now) != InvitationStatus.Expired) throw new InvitationNotPendingException(Id, StatusAt(now));
        Status = InvitationStatus.Expired;
        UpdatedAt = now;
    }

    private void RequireOpenAt(DateTime now)
    {
        switch (StatusAt(now))
        {
            case InvitationStatus.Pending:
                return;
            case InvitationStatus.Expired:
                throw new InvitationExpiredException(Id);
            default:
                throw new InvitationNotPendingException(Id, Status);
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();
}
