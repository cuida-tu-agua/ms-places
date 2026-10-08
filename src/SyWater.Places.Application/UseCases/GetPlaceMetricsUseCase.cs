using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;

namespace SyWater.Places.Application.UseCases;

public sealed class GetPlaceMetricsUseCase(IPlaceRepository places) : IGetPlaceMetricsUseCase
{
    public async Task<PlaceMetrics> ExecuteAsync(CancellationToken ct) =>
        new(await places.CountActiveAsync(ct));
}
