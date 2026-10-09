using SyWater.Places.Domain.Common;

namespace SyWater.Places.Domain.Tariffs;

/// <summary>Invalid tariff data (price out of range, unknown stratum...). HTTP 400.</summary>
public sealed class InvalidTariffException(string message)
    : DomainException("tariff.invalid", message);
