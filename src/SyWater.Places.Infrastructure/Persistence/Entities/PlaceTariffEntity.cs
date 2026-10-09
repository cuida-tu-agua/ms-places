using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Places.Infrastructure.Persistence.Entities;

[Table("place_tariffs", Schema = "places")]
public sealed class PlaceTariffEntity
{
    [Key, Column("id"), DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("source")] public string Source { get; set; } = "";
    [Column("unit_price", TypeName = "decimal(12,2)")] public decimal? UnitPrice { get; set; }
    [Column("fixed_charge", TypeName = "decimal(12,2)")] public decimal? FixedCharge { get; set; }
    [Column("stratum")] public byte? Stratum { get; set; }
    [Column("valid_from")] public DateTime ValidFrom { get; set; }
    [Column("created_by")] public Guid CreatedBy { get; set; }
}
