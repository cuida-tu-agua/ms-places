using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SyWater.Places.Infrastructure.Persistence.Entities;

[Table("tips", Schema = "places")]
public sealed class TipEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("title")] public string Title { get; set; } = "";
    [Column("body")] public string Body { get; set; } = "";
    [Column("category")] public string Category { get; set; } = "";
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("created_by")] public Guid? CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_by")] public Guid? UpdatedBy { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("tip_favorites", Schema = "places")]
[PrimaryKey(nameof(UserId), nameof(TipId))]
public sealed class TipFavoriteEntity
{
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("tip_id")] public Guid TipId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
