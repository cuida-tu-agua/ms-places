using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Places.Infrastructure.Persistence.Entities;


[Table("places", Schema = "places")]
public sealed class PlaceEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("city_id")] public Guid CityId { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("place_type")] public string PlaceType { get; set; } = "";
    [Column("address")] public string Address { get; set; } = "";
    [Column("currency")] public string Currency { get; set; } = "";
    [Column("measurement_unit")] public string MeasurementUnit { get; set; } = "";
    [Column("is_default")] public bool IsDefault { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}