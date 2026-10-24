using Microsoft.EntityFrameworkCore;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Infrastructure.Consumption;
using SyWater.Places.Infrastructure.Devices;
using SyWater.Places.Infrastructure.Iam;
using SyWater.Places.Infrastructure.Persistence;
using SyWater.Places.Infrastructure.Sharing;

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
        services.AddScoped<ISetCatalogTariffUseCase, SetCatalogTariffUseCase>();
        services.AddScoped<IGetTariffCatalogUseCase, GetTariffCatalogUseCase>();
        services.AddScoped<IGetPlaceCostUseCase, GetPlaceCostUseCase>();
        services.AddScoped<ICreateTipUseCase, CreateTipUseCase>();
        services.AddScoped<IEditTipUseCase, EditTipUseCase>();
        services.AddScoped<ISetTipActiveUseCase, SetTipActiveUseCase>();
        services.AddScoped<IListAdminTipsUseCase, ListAdminTipsUseCase>();
        services.AddScoped<IGetTipsUseCase, GetTipsUseCase>();
        services.AddScoped<IManageTipFavoritesUseCase, ManageTipFavoritesUseCase>();

        // Sharing (HU-039 to HU-047)
        services.AddScoped<IPlaceAccessService, PlaceAccessService>();
        services.AddScoped<IGetPlaceAccessUseCase, GetPlaceAccessUseCase>();
        services.AddScoped<IListSharedPlacesUseCase, ListSharedPlacesUseCase>();
        services.AddScoped<IGetPlaceRecipientsUseCase, GetPlaceRecipientsUseCase>();
        services.AddScoped<ICreateGuestLinkUseCase, CreateGuestLinkUseCase>();
        services.AddScoped<IGetGuestLinkUseCase, GetGuestLinkUseCase>();
        services.AddScoped<IDeactivateGuestLinkUseCase, DeactivateGuestLinkUseCase>();
        services.AddScoped<IResolveGuestLinkUseCase, ResolveGuestLinkUseCase>();
        services.AddScoped<IInviteCollaboratorUseCase, InviteCollaboratorUseCase>();
        services.AddScoped<IListInvitationsUseCase, ListInvitationsUseCase>();
        services.AddScoped<ICancelInvitationUseCase, CancelInvitationUseCase>();
        services.AddScoped<IListMyInvitationsUseCase, ListMyInvitationsUseCase>();
        services.AddScoped<IAnswerInvitationUseCase, AnswerInvitationUseCase>();
        services.AddScoped<IListMembersUseCase, ListMembersUseCase>();
        services.AddScoped<IChangeMemberPermissionsUseCase, ChangeMemberPermissionsUseCase>();
        services.AddScoped<IRevokeMemberUseCase, RevokeMemberUseCase>();
        services.AddScoped<ILeavePlaceUseCase, LeavePlaceUseCase>();
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
        services.AddScoped<ITariffCatalog, EfTariffCatalog>();
        services.AddScoped<ISharingRepository, EfSharingRepository>();
        services.AddSingleton<IGuestTokenGenerator, RandomGuestTokenGenerator>();
        services.AddScoped<EfTipRepository>();
        services.AddScoped<ITipRepository>(sp => sp.GetRequiredService<EfTipRepository>());
        services.AddScoped<ITipFavoriteRepository>(sp => sp.GetRequiredService<EfTipRepository>());

        // The caller's token travels to the other services that answer on behalf of the user (device, consumption)
        services.AddHttpContextAccessor();
        services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();

        var consumptionUrl = config["Services:ConsumptionBaseUrl"];
        if (string.IsNullOrWhiteSpace(consumptionUrl))
            throw new InvalidOperationException("Missing 'Services:ConsumptionBaseUrl'.");
        services.AddHttpClient<IPlaceConsumptionReader, HttpPlaceConsumptionReader>(client =>
        {
            client.BaseAddress = new Uri(consumptionUrl.EndsWith('/') ? consumptionUrl : consumptionUrl + "/");
            // The first cost query after ms-consumption starts can take ~5 s (cold start). 8 s by default, still below the
            // 10 s the app waits, so a really slow ms-consumption is answered with 503 and not with a cut connection.
            client.Timeout = TimeSpan.FromSeconds(config.GetValue("Services:ConsumptionTimeoutSeconds", 8));
        });

        var devicesUrl = config["Services:DevicesBaseUrl"];
        if (string.IsNullOrWhiteSpace(devicesUrl))
        {
            services.AddSingleton<IDeviceLinkChecker, DeviceServiceNotDeployedLinkChecker>();
        }
        else
        {
            services.AddHttpClient<IDeviceLinkChecker, HttpDeviceLinkChecker>(client =>
            {
                client.BaseAddress = new Uri(devicesUrl.EndsWith('/') ? devicesUrl : devicesUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        // ms-iam: finds the people behind an id or an e-mail and sends the invitation e-mail. Same shared key as the others
        // Without the URL or the key (a local run that only sets the connection string) sharing answers 503 instead of
        // keeping the whole service from starting.
        var iamUrl = config["Services:IamBaseUrl"];
        var internalKey = config["Internal:ApiKey"];
        if (string.IsNullOrWhiteSpace(iamUrl) || string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 24)
        {
            services.AddSingleton<IUserDirectory, IamNotConfiguredUserDirectory>();
            services.AddSingleton<IInvitationNotifier, IamNotConfiguredInvitationNotifier>();
        }
        else
        {
            var iamBase = new Uri(iamUrl.EndsWith('/') ? iamUrl : iamUrl + "/");
            services.AddHttpClient<IUserDirectory, HttpUserDirectory>((http, _) =>
            {
                http.BaseAddress = iamBase;
                http.Timeout = TimeSpan.FromSeconds(5);
                return new HttpUserDirectory(http, internalKey);
            });
            services.AddHttpClient<IInvitationNotifier, HttpInvitationNotifier>((http, _) =>
            {
                http.BaseAddress = iamBase;
                http.Timeout = TimeSpan.FromSeconds(5);
                return new HttpInvitationNotifier(http, internalKey);
            });
        }

        return services;
    }
}
