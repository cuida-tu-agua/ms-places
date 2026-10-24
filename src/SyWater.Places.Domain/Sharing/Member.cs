namespace SyWater.Places.Domain.Sharing;

/// <summary>
/// A collaborator: a registered user the owner invited to a place, with the permissions the owner chose (HU-043/044).
/// The owner is NOT a member (it is <c>Place.OwnerId</c>). Revoking keeps the row, so nothing is lost and a later
/// invitation can bring the person back.
/// </summary>
public sealed class Member
{
    public Guid Id { get; }
    public Guid PlaceId { get; }
    public Guid UserId { get; }
    public PermissionSet Permissions { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    private Member(Guid id, Guid placeId, Guid userId, PermissionSet permissions,
        DateTime createdAt, DateTime updatedAt, DateTime? revokedAt)
    {
        Id = id;
        PlaceId = placeId;
        UserId = userId;
        Permissions = permissions;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RevokedAt = revokedAt;
    }

    public static Member Create(Guid placeId, Guid userId, PermissionSet permissions, DateTime now)
    {
        if (placeId == Guid.Empty) throw new InvalidInvitationException("Place is required.");
        if (userId == Guid.Empty) throw new InvalidInvitationException("User is required.");
        return new Member(Guid.NewGuid(), placeId, userId, permissions, now, now, null);
    }

    public static Member Restore(Guid id, Guid placeId, Guid userId, PermissionSet permissions,
        DateTime createdAt, DateTime updatedAt, DateTime? revokedAt) =>
        new(id, placeId, userId, permissions, createdAt, updatedAt, revokedAt);

    /// <summary>HU-044: the new permissions apply at once (every request asks ms-places again).</summary>
    public void ChangePermissions(PermissionSet permissions, DateTime now)
    {
        if (!IsActive) throw new MemberNotFoundException(Id);
        Permissions = permissions;
        UpdatedAt = now;
    }

    /// <summary>HU-046: the access is cut at once, even if the person has a session open.</summary>
    public void Revoke(DateTime now)
    {
        if (!IsActive) throw new MemberNotFoundException(Id);
        RevokedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// The person accepted a new invitation: they come back (if they were revoked) with the permissions of that
    /// invitation. If they were still active, the newer invitation simply sets the permissions.
    /// </summary>
    public void Rejoin(PermissionSet permissions, DateTime now)
    {
        Permissions = permissions;
        RevokedAt = null;
        UpdatedAt = now;
    }
}
