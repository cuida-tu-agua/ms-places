using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Places.Infrastructure.Persistence.Entities;


[Table("countries", Schema = "places")]
public sealed class CountryEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("default_currency")] public string DefaultCurrency { get; set; } = "";
    [Column("default_unit")] public string DefaultUnit { get; set; } = "";
}

[Table("subdivisions", Schema = "places")]
public sealed class SubdivisionEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("country_id")] public Guid CountryId { get; set; }
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";

    [ForeignKey(nameof(CountryId))] public CountryEntity Country { get; set; } = null!;
}

[Table("cities", Schema = "places")]
public sealed class CityEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("subdivision_id")] public Guid SubdivisionId { get; set; }
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";

    [ForeignKey(nameof(SubdivisionId))] public SubdivisionEntity Subdivision { get; set; } = null!;
}