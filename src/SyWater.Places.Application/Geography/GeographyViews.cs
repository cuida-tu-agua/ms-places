using SyWater.Places.Domain.Places;

namespace SyWater.Places.Application.Geography;

public sealed record CountryView(string Code, string Name, Currency DefaultCurrency, MeasurementUnit DefaultUnit);

public sealed record SubdivisionView(Guid Id, string Code, string Name);

public sealed record CityView(Guid Id, string Code, string Name);

public sealed record CityLocation(
    Guid CityId,
    string CityName,
    Guid SubdivisionId,
    string SubdivisionName,
    string CountryCode,
    string CountryName,
    Currency DefaultCurrency,
    MeasurementUnit DefaultUnit);