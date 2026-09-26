using SyWater.Places.Domain.Places;

namespace SyWater.Places.Domain.Tests;

public class PlaceTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static Place NewPlace(string name = "Casa Bogotá", string address = "Cra 7 # 12-34") =>
        Place.Create(Guid.NewGuid(), Guid.NewGuid(), name, PlaceType.Residential, address,
                     Currency.Cop, MeasurementUnit.Liters, isDefault: true, Now);

    // ── HU-008: register ─────────────────────────────────────────────

    [Fact]
    public void Create_trims_text_and_sets_timestamps()
    {
        var place = NewPlace(name: "  Casa  ", address: "  Cra 7  ");

        Assert.Equal("Casa", place.Name);
        Assert.Equal("Cra 7", place.Address);
        Assert.Equal(Now, place.CreatedAt);
        Assert.Equal(Now, place.UpdatedAt);
        Assert.False(place.IsDeleted);
        Assert.NotEqual(Guid.Empty, place.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_name(string name)
    {
        Assert.Throws<InvalidPlaceException>(() => NewPlace(name: name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_address(string address)
    {
        Assert.Throws<InvalidPlaceException>(() => NewPlace(address: address));
    }

    [Fact]
    public void Create_accepts_name_of_exactly_200_and_rejects_201()
    {
        var ok = NewPlace(name: new string('a', Place.NameMaxLength));
        Assert.Equal(Place.NameMaxLength, ok.Name.Length);

        Assert.Throws<InvalidPlaceException>(() => NewPlace(name: new string('a', Place.NameMaxLength + 1)));
    }

    [Fact]
    public void Create_rejects_empty_city_and_owner()
    {
        Assert.Throws<InvalidPlaceException>(() =>
            Place.Create(Guid.NewGuid(), Guid.Empty, "Casa", PlaceType.Residential, "Cra 7",
                         Currency.Cop, MeasurementUnit.Liters, isDefault: false, Now));
        Assert.Throws<InvalidPlaceException>(() =>
            Place.Create(Guid.Empty, Guid.NewGuid(), "Casa", PlaceType.Residential, "Cra 7",
                         Currency.Cop, MeasurementUnit.Liters, isDefault: false, Now));
    }

    // ── HU-009: edit ─────────────────────────────────────────────────

    [Fact]
    public void Update_changes_data_and_updated_at_but_not_created_at()
    {
        var place = NewPlace();
        var newCity = Guid.NewGuid();
        var later = Now.AddDays(1);

        place.Update("Local", PlaceType.Commercial, "Av. Amazonas", newCity, Currency.Usd, MeasurementUnit.CubicMeters, later);

        Assert.Equal("Local", place.Name);
        Assert.Equal(PlaceType.Commercial, place.Type);
        Assert.Equal("Av. Amazonas", place.Address);
        Assert.Equal(newCity, place.CityId);
        Assert.Equal(Currency.Usd, place.Currency);
        Assert.Equal(MeasurementUnit.CubicMeters, place.MeasurementUnit);
        Assert.Equal(later, place.UpdatedAt);
        Assert.Equal(Now, place.CreatedAt);
    }

    [Fact]
    public void Update_with_invalid_name_changes_nothing()
    {
        var place = NewPlace();

        Assert.Throws<InvalidPlaceException>(() =>
            place.Update("   ", PlaceType.Commercial, "Otra", Guid.NewGuid(), Currency.Cop, MeasurementUnit.Liters, Now.AddDays(1)));

        Assert.Equal("Casa Bogotá", place.Name);
        Assert.Equal(Now, place.UpdatedAt);
    }

    [Fact]
    public void Deleted_place_cannot_be_updated()
    {
        var deleted = Place.Restore(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Casa", PlaceType.Residential,
                                    "Cra 7", Currency.Cop, MeasurementUnit.Liters, false, Now, Now, deletedAt: Now);

        Assert.Throws<PlaceNotFoundException>(() =>
            deleted.Update("X", PlaceType.Commercial, "Y", Guid.NewGuid(), Currency.Cop, MeasurementUnit.Liters, Now));
    }
}   