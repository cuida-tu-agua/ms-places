using SyWater.Places.Domain.Common;

namespace SyWater.Places.Domain.Tariffs;

/// <summary>Invalid tariff data (price out of range, unknown stratum...). HTTP 400.</summary>
public sealed class InvalidTariffException(string message)
    : DomainException("tariff.invalid", message);

/// <summary>The city has no preloaded tariff for that stratum. HTTP 404.</summary>
public sealed class TariffCatalogUnavailableException(Guid cityId, int? stratum)
    : DomainException("tariff.catalog_unavailable",
        stratum is null
            ? $"City {cityId} has no preloaded tariffs."
            : $"City {cityId} has no preloaded tariff for stratum {stratum}.");
