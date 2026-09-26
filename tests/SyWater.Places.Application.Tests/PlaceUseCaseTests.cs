using SyWater.Places.Application.Places;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Tests;

public class PlaceUseCasesTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _owner = Guid.NewGuid();

    private CreatePlaceUseCase Create => new(_places, _geography, _clock);
    private UpdatePlaceUseCase Update => new(_places, _geography, _clock);
    private GetPlaceUseCase Get => new(_places, _geography);

    private async Task<PlaceView> CreatePlace(string name, Guid cityId, MeasurementUnit? unit = null, Guid? owner = null)
    {
        var view = await Create.ExecuteAsync(
            new CreatePlaceCommand(owner ?? _owner, cityId, name, PlaceType.Residential, "Calle 1", unit),
            CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return view;
    }

    private Task<PlaceView> UpdatePlace(Guid placeId, Guid cityId, string name = "Casa", Guid? owner = null) =>
        Update.ExecuteAsync(
            new UpdatePlaceCommand(owner ?? _owner, placeId, cityId, name, PlaceType.Commercial,
                                   "Av. Siempre Viva 742", MeasurementUnit.CubicMeters),
            CancellationToken.None);

    // ── HU-008: register a place ────────────────────────────────────

    [Fact]
    public async Task First_place_is_default_and_second_is_not()
    {
        var first = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);
        var second = await CreatePlace("Finca", FakeGeographyReader.Medellin.CityId);

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
        Assert.Equal(2, _places.Items.Count);
    }

    [Fact]
    public async Task Default_is_per_owner()
    {
        await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);
        var otherOwnersFirst = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId, owner: Guid.NewGuid());

        Assert.True(otherOwnersFirst.IsDefault);
    }

    [Fact]
    public async Task Currency_and_location_come_from_the_city()
    {
        var bogota = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);
        var quito = await CreatePlace("Local", FakeGeographyReader.Quito.CityId);

        Assert.Equal(Currency.Cop, bogota.Currency);
        Assert.Equal(Currency.Usd, quito.Currency);
        Assert.Equal("EC", quito.CountryCode);
        Assert.Equal("Pichincha", quito.SubdivisionName);
        Assert.Equal(FakeGeographyReader.Quito.SubdivisionId, quito.SubdivisionId);
    }

    [Fact]
    public async Task Unit_defaults_to_country_unit_unless_given()
    {
        var byDefault = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);
        var chosen = await CreatePlace("Finca", FakeGeographyReader.Bogota.CityId, MeasurementUnit.Gallons);

        Assert.Equal(MeasurementUnit.Liters, byDefault.MeasurementUnit);
        Assert.Equal(MeasurementUnit.Gallons, chosen.MeasurementUnit);
    }

    [Fact]
    public async Task Unknown_city_is_rejected_and_nothing_is_saved()
    {
        await Assert.ThrowsAsync<CityNotFoundException>(() => CreatePlace("Casa", Guid.NewGuid()));
        Assert.Empty(_places.Items);
    }

    // ── HU-009: edit a place ────────────────────────────────────────

    [Fact]
    public async Task Edit_changes_the_data_and_keeps_currency_in_the_same_country()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);

        var updated = await UpdatePlace(place.Id, FakeGeographyReader.Medellin.CityId, name: "Casa Medellín");

        Assert.Equal("Casa Medellín", updated.Name);
        Assert.Equal(PlaceType.Commercial, updated.Type);
        Assert.Equal("Medellín", updated.CityName);
        Assert.Equal(Currency.Cop, updated.Currency);
        Assert.Equal(MeasurementUnit.CubicMeters, updated.MeasurementUnit);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
        Assert.Equal(1, _places.UpdateCalls);
    }

    [Fact]
    public async Task Moving_a_place_to_another_country_changes_its_currency()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);

        var updated = await UpdatePlace(place.Id, FakeGeographyReader.Quito.CityId);

        Assert.Equal(Currency.Usd, updated.Currency);
        Assert.Equal("EC", updated.CountryCode);
    }

    [Fact]
    public async Task Edit_keeps_is_default()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);

        var updated = await UpdatePlace(place.Id, FakeGeographyReader.Bogota.CityId);

        Assert.True(updated.IsDefault);
    }

    [Fact]
    public async Task Another_owner_cannot_read_or_edit_my_place()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);
        var stranger = Guid.NewGuid();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() =>
            UpdatePlace(place.Id, FakeGeographyReader.Bogota.CityId, owner: stranger));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() =>
            Get.ExecuteAsync(stranger, place.Id, CancellationToken.None));
        Assert.Equal(0, _places.UpdateCalls);
    }

    [Fact]
    public async Task Edit_with_unknown_city_is_rejected()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Bogota.CityId);

        await Assert.ThrowsAsync<CityNotFoundException>(() => UpdatePlace(place.Id, Guid.NewGuid()));
        Assert.Equal(0, _places.UpdateCalls);
    }

    [Fact]
    public async Task Get_returns_the_place_with_its_location()
    {
        var place = await CreatePlace("Casa", FakeGeographyReader.Medellin.CityId);

        var loaded = await Get.ExecuteAsync(_owner, place.Id, CancellationToken.None);

        Assert.Equal(place.Id, loaded.Id);
        Assert.Equal("Antioquia", loaded.SubdivisionName);
        Assert.Equal(FakeGeographyReader.Medellin.SubdivisionId, loaded.SubdivisionId);
    }
}