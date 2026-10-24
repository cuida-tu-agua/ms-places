using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Places.Infrastructure.Persistence.Entities;

[Table("place_members", Schema = "places")]
public sealed class PlaceMemberEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("role")] public string Role { get; set; } = "COLLABORATOR";
    [Column("permissions")] public string Permissions { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
}

[Table("invitations", Schema = "places")]
public sealed class InvitationEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("invited_by")] public Guid InvitedBy { get; set; }
    [Column("email")] public string Email { get; set; } = "";
    [Column("permissions")] public string Permissions { get; set; } = "";
    [Column("status")] public string Status { get; set; } = "PENDING";
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("responded_at")] public DateTime? RespondedAt { get; set; }
}

[Table("guest_links", Schema = "places")]
public sealed class GuestLinkEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("token")] public string Token { get; set; } = "";
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("deactivated_at")] public DateTime? DeactivatedAt { get; set; }
}
