using System.ComponentModel.DataAnnotations;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Api.Contracts;

public sealed class SetManualTariffRequest
{
    [Required, Range(0.01, (double)PlaceTariff.MaxUnitPrice)] public decimal? UnitPricePerM3 { get; init; }

    [Range(0, (double)PlaceTariff.MaxFixedCharge)] public decimal? FixedMonthlyCharge { get; init; }
}
