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
