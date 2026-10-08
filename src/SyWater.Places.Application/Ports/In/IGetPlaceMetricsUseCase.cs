using SyWater.Places.Application.Places;

namespace SyWater.Places.Application.Ports.In;

/// <summary>HU-062: the place total of the administrator's dashboard (asked by ms-iam).</summary>
public interface IGetPlaceMetricsUseCase
{
    Task<PlaceMetrics> ExecuteAsync(CancellationToken ct);
}
