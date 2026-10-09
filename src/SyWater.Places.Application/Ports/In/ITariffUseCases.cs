using SyWater.Places.Application.Tariffs;

namespace SyWater.Places.Application.Ports.In;

/// <summary>HU-054: the tariff in force and the history of a place of the user.</summary>
public interface IGetPlaceTariffsUseCase
{
    Task<PlaceTariffsView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

/// <summary>HU-054: the user types the price per m³ of their bill (and optionally a monthly fixed charge).</summary>
public interface ISetManualTariffUseCase
{
    Task<TariffView> ExecuteAsync(SetManualTariffCommand command, CancellationToken ct);
}

/// <summary>HU-066 / HU-069: the user picks the stratum and the prices of the city catalog are used.</summary>
public interface ISetCatalogTariffUseCase
{
    Task<TariffView> ExecuteAsync(SetCatalogTariffCommand command, CancellationToken ct);
}

/// <summary>HU-066: the preloaded tariffs of a city, by stratum.</summary>
public interface IGetTariffCatalogUseCase
{
    Task<TariffCatalogView> ExecuteAsync(Guid cityId, CancellationToken ct);
}

/// <summary>HU-056: what the water of a day, week or month costs (an estimate).</summary>
public interface IGetPlaceCostUseCase
{
    Task<CostEstimateView> ExecuteAsync(Guid ownerId, Guid placeId, string? period, string? timeZone, CancellationToken ct);
}
