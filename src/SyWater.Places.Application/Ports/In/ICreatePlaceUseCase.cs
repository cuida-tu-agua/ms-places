using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Places;

namespace SyWater.Places.Application.Ports.In;


public interface ICreatePlaceUseCase
{
    Task<PlaceView> ExecuteAsync(CreatePlaceCommand command, CancellationToken ct);
}


public interface IGetPlaceUseCase
{
    Task<PlaceView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}


public interface IUpdatePlaceUseCase
{
    Task<PlaceView> ExecuteAsync(UpdatePlaceCommand command, CancellationToken ct);
}

public interface IListPlacesUseCase
{
    Task<IReadOnlyList<PlaceView>> ExecuteAsync(Guid ownerId, CancellationToken ct);
}

public interface ISelectPlaceUseCase
{
    Task<PlaceView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

public interface IDeletePlaceUseCase
{
    Task ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct);
}

public interface IGeographyQueries
{
    Task<IReadOnlyList<CountryView>> ListCountriesAsync(CancellationToken ct);
    Task<IReadOnlyList<SubdivisionView>> ListSubdivisionsAsync(string countryCode, CancellationToken ct);
    Task<IReadOnlyList<CityView>> ListCitiesAsync(Guid subdivisionId, CancellationToken ct);
}