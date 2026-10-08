using SyWater.Places.Application.Places;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Tests;

/// <summary>HU-062: the place total of the administrator's dashboard.</summary>
public class PlaceMetricsTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeDeviceLinkChecker _devices = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    private async Task<PlaceView> CreatePlace(Guid owner, string name)
    {
        var view = await new CreatePlaceUseCase(_places, _geography, _clock).ExecuteAsync(
            new CreatePlaceCommand(owner, FakeGeographyReader.Bogota.CityId, name, PlaceType.Residential, "Calle 1", null),
            CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return view;
    }

    [Fact]
    public async Task Counts_the_places_of_every_owner_and_skips_the_deleted_ones()
    {
        var ana = Guid.NewGuid();
        await CreatePlace(ana, "Casa");
        var finca = await CreatePlace(ana, "Finca");
        await CreatePlace(Guid.NewGuid(), "Local");
        await new DeletePlaceUseCase(_places, _devices, _clock).ExecuteAsync(ana, finca.Id, CancellationToken.None);

        var metrics = await new GetPlaceMetricsUseCase(_places).ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, metrics.Total);
    }

    [Fact]
    public async Task With_no_places_the_total_is_zero()
    {
        Assert.Equal(0, (await new GetPlaceMetricsUseCase(_places).ExecuteAsync(CancellationToken.None)).Total);
    }
}
