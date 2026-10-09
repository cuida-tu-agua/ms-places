using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Places.Infrastructure.Persistence.Entities;

[Table("tariff_catalog", Schema = "places")]
public sealed class TariffCatalogEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("city_id")] public Guid CityId { get; set; }
    [Column("stratum")] public byte Stratum { get; set; }
    [Column("currency")] public string Currency { get; set; } = "";
    [Column("fixed_charge", TypeName = "decimal(12,2)")] public decimal FixedCharge { get; set; }
    [Column("basic_price", TypeName = "decimal(12,2)")] public decimal BasicPrice { get; set; }
    [Column("complementary_price", TypeName = "decimal(12,2)")] public decimal ComplementaryPrice { get; set; }
    [Column("luxury_price", TypeName = "decimal(12,2)")] public decimal LuxuryPrice { get; set; }
    [Column("basic_limit_m3", TypeName = "decimal(6,2)")] public decimal BasicLimitM3 { get; set; }
    [Column("complementary_limit_m3", TypeName = "decimal(6,2)")] public decimal ComplementaryLimitM3 { get; set; }
    [Column("effective_from")] public DateOnly EffectiveFrom { get; set; }
    [Column("source")] public string Source { get; set; } = "";
}
