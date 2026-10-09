using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tariffs;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.Tests;

/// <summary>In-memory adapter of the append-only tariff history.</summary>
internal sealed class InMemoryPlaceTariffRepository : IPlaceTariffRepository
{
    public List<PlaceTariff> Items { get; } = [];

    public Task<IReadOnlyList<PlaceTariff>> ListAsync(Guid placeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PlaceTariff>>(Items.Where(t => t.PlaceId == placeId)
            .OrderByDescending(t => t.ValidFrom).ThenByDescending(t => t.Id).ToList());

    public async Task<PlaceTariff?> GetInForceAsync(Guid placeId, DateTime at, CancellationToken ct) =>
        (await ListAsync(placeId, ct)).FirstOrDefault(t => t.ValidFrom <= at);

    public Task<PlaceTariff> AddAsync(PlaceTariff tariff, CancellationToken ct)
    {
        var saved = PlaceTariff.Restore(Items.Count + 1, tariff.PlaceId, tariff.Source, tariff.UnitPricePerM3,
            tariff.FixedMonthlyCharge, tariff.Stratum, tariff.ValidFrom, tariff.CreatedBy);
        Items.Add(saved);
        return Task.FromResult(saved);
    }
}

/// <summary>HU-054: the user types the price per m³ of their bill; the history keeps the previous ones.</summary>
public class PlaceTariffTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly InMemoryPlaceTariffRepository _tariffs = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _owner = Guid.NewGuid();

    private SetManualTariffUseCase Set => new(_places, _tariffs, _clock);
    private GetPlaceTariffsUseCase Get => new(_places, _tariffs);

    private async Task<PlaceView> CreatePlace(Guid? owner = null)
    {
        var view = await new CreatePlaceUseCase(_places, _geography, _clock).ExecuteAsync(
            new CreatePlaceCommand(owner ?? _owner, FakeGeographyReader.Bogota.CityId, "Casa", PlaceType.Residential,
                "Calle 1", null), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return view;
    }

    private Task<TariffView> SetPrice(Guid placeId, decimal price, decimal? fixedCharge = null, Guid? owner = null) =>
        Set.ExecuteAsync(new SetManualTariffCommand(owner ?? _owner, placeId, price, fixedCharge), CancellationToken.None);

    [Fact]
    public async Task A_place_without_tariff_has_no_current_one_so_the_app_can_invite_to_set_it()
    {
        var place = await CreatePlace();

        var view = await Get.ExecuteAsync(_owner, place.Id, CancellationToken.None);

        Assert.Null(view.Current);
        Assert.Empty(view.History);
    }

    [Fact]
    public async Task The_user_types_the_price_and_an_optional_fixed_charge()
    {
        var place = await CreatePlace();

        var saved = await SetPrice(place.Id, 5234.567m, 12000m);

        Assert.Equal(TariffSource.Manual, saved.Source);
        Assert.Equal(5234.57m, saved.UnitPricePerM3);     // two decimals
        Assert.Equal(12000m, saved.FixedMonthlyCharge);
        Assert.Null(saved.Stratum);
        Assert.Equal(Currency.Cop, saved.Currency);       // the currency of the place
        var view = await Get.ExecuteAsync(_owner, place.Id, CancellationToken.None);
        Assert.Equal(saved.Id, view.Current!.Id);
    }

    [Fact]
    public async Task The_fixed_charge_is_optional()
    {
        var place = await CreatePlace();

        var saved = await SetPrice(place.Id, 4000m);

        Assert.Null(saved.FixedMonthlyCharge);
    }

    [Fact]
    public async Task Updating_the_tariff_keeps_the_previous_ones_in_the_history()
    {
        var place = await CreatePlace();
        await SetPrice(place.Id, 4000m);
        _clock.Advance(TimeSpan.FromDays(30));
        var second = await SetPrice(place.Id, 4500m, 9000m);

        var view = await Get.ExecuteAsync(_owner, place.Id, CancellationToken.None);

        Assert.Equal(second.Id, view.Current!.Id);
        Assert.Equal(4500m, view.Current.UnitPricePerM3);
        Assert.Equal([4500m, 4000m], view.History.Select(h => h.UnitPricePerM3!.Value));
        Assert.Equal(2, _tariffs.Items.Count);            // nothing was overwritten
    }

    [Fact]
    public async Task The_tariff_in_force_at_a_past_date_is_still_the_old_one()
    {
        var place = await CreatePlace();
        await SetPrice(place.Id, 4000m);
        var beforeChange = _clock.GetUtcNow().UtcDateTime.AddDays(10);
        _clock.Advance(TimeSpan.FromDays(30));
        await SetPrice(place.Id, 4500m);

        var inForce = await _tariffs.GetInForceAsync(place.Id, beforeChange, CancellationToken.None);

        Assert.Equal(4000m, inForce!.UnitPricePerM3);     // HU-054: past calculations are not altered
    }

    [Fact]
    public async Task Saving_the_same_prices_again_does_not_add_a_duplicate()
    {
        var place = await CreatePlace();
        var first = await SetPrice(place.Id, 4000m, 8000m);
        _clock.Advance(TimeSpan.FromDays(1));

        var again = await SetPrice(place.Id, 4000m, 8000m);

        Assert.Equal(first.Id, again.Id);
        Assert.Single(_tariffs.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_001)]
    public async Task A_price_outside_the_range_is_rejected(decimal price)
    {
        var place = await CreatePlace();

        await Assert.ThrowsAsync<InvalidTariffException>(() => SetPrice(place.Id, price));
        Assert.Empty(_tariffs.Items);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_000_001)]
    public async Task A_fixed_charge_outside_the_range_is_rejected(decimal fixedCharge)
    {
        var place = await CreatePlace();

        await Assert.ThrowsAsync<InvalidTariffException>(() => SetPrice(place.Id, 4000m, fixedCharge));
    }

    [Fact]
    public async Task Nobody_can_read_or_change_the_tariff_of_a_place_of_someone_else()
    {
        var place = await CreatePlace();
        var stranger = Guid.NewGuid();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => SetPrice(place.Id, 4000m, owner: stranger));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get.ExecuteAsync(stranger, place.Id, CancellationToken.None));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => SetPrice(Guid.NewGuid(), 4000m));
        Assert.Empty(_tariffs.Items);
    }

    [Fact]
    public async Task The_tariff_belongs_to_each_place()
    {
        var casa = await CreatePlace();
        var finca = await CreatePlace();
        await SetPrice(casa.Id, 4000m);

        Assert.Null((await Get.ExecuteAsync(_owner, finca.Id, CancellationToken.None)).Current);
    }
}
