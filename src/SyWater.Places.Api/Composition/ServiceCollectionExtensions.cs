using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Infrastructure.Persistence;

namespace SyWater.Places.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlacesApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICreatePlaceUseCase, CreatePlaceUseCase>();
        services.AddScoped<IGetPlaceUseCase, GetPlaceUseCase>();
        services.AddScoped<IUpdatePlaceUseCase, UpdatePlaceUseCase>();
        services.AddScoped<IGeographyQueries, GeographyQueries>();
        return services;
    }

    public static IServiceCollection AddPlacesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Places")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Places'.");

        services.AddDbContext<PlacesDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IPlaceRepository, EfPlaceRepository>();
        services.AddScoped<IGeographyReader, EfGeographyReader>();
        return services;
    }
}