namespace SyWater.Places.Application.Ports.Out;

/// <summary>What ms-consumption reports for a place and a period.</summary>
public sealed record PeriodConsumption(DateTime From, DateTime To, decimal TotalLiters, bool HasData);

/// <summary>Outbound port: the water a place used (ms-consumption). Called with the token of the user who asks.</summary>
public interface IPlaceConsumptionReader
{
    /// <param name="period">day, week or month (what ms-consumption accepts).</param>
    /// <exception cref="ExternalServiceUnavailableException">ms-consumption did not answer.</exception>
    Task<PeriodConsumption> GetAsync(Guid placeId, string period, string? timeZone, CancellationToken ct);
}
