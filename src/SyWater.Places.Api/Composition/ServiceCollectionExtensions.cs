using Microsoft.EntityFrameworkCore;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Infrastructure.Devices;
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
        services.AddScoped<IListPlacesUseCase, ListPlacesUseCase>();
        services.AddScoped<ISelectPlaceUseCase, SelectPlaceUseCase>();
        services.AddScoped<IDeletePlaceUseCase, DeletePlaceUseCase>();
        services.AddScoped<IGeographyQueries, GeographyQueries>();
        services.AddScoped<IGetPlaceMetricsUseCase, GetPlaceMetricsUseCase>();
        services.AddScoped<IGetPlaceTariffsUseCase, GetPlaceTariffsUseCase>();
        services.AddScoped<ISetManualTariffUseCase, SetManualTariffUseCase>();
        return services;
    }

    public static IServiceCollection AddPlacesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Places")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Places'.");

        services.AddDbContext<PlacesDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IPlaceRepository, EfPlaceRepository>();
        services.AddScoped<IGeographyReader, EfGeographyReader>();
        services.AddScoped<IPlaceTariffRepository, EfPlaceTariffRepository>();

        var devicesUrl = config["Services:DevicesBaseUrl"];
        if (string.IsNullOrWhiteSpace(devicesUrl))
        {
            services.AddSingleton<IDeviceLinkChecker, DeviceServiceNotDeployedLinkChecker>();
        }
        else
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();
            services.AddHttpClient<IDeviceLinkChecker, HttpDeviceLinkChecker>(client =>
            {
                client.BaseAddress = new Uri(devicesUrl.EndsWith('/') ? devicesUrl : devicesUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        return services;
    }
}
