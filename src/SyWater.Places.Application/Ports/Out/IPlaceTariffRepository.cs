using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Ports.Out;

/// <summary>Outbound port: the tariff history of a place. Append-only, there is no update or delete.</summary>
public interface IPlaceTariffRepository
{
    /// <summary>Every entry of the place, newest first.</summary>
    Task<IReadOnlyList<PlaceTariff>> ListAsync(Guid placeId, CancellationToken ct);

    /// <summary>The entry in force at <paramref name="at"/>: the newest one with ValidFrom &lt;= at, or null.</summary>
    Task<PlaceTariff?> GetInForceAsync(Guid placeId, DateTime at, CancellationToken ct);

    /// <summary>Appends the entry and returns it with its generated id.</summary>
    Task<PlaceTariff> AddAsync(PlaceTariff tariff, CancellationToken ct);
}
