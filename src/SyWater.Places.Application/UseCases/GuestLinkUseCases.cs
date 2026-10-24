using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.UseCases;

/// <summary>HU-039. Only the owner shares a place with guests.</summary>
public sealed class CreateGuestLinkUseCase(
    IPlaceRepository places, ISharingRepository sharing, IGuestTokenGenerator tokens, TimeProvider clock)
    : ICreateGuestLinkUseCase
{
    public async Task<GuestLinkView> ExecuteAsync(Guid ownerId, Guid placeId, bool regenerate, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var current = await sharing.GetActiveGuestLinkAsync(place.Id, ct);
        if (current is not null && !regenerate)
            return GuestLinkView.From(current);   // the same link every time, so a copied link keeps working

        var now = clock.GetUtcNow().UtcDateTime;
        var link = GuestLink.Create(place.Id, ownerId, tokens.NewToken(), now);
        current?.Deactivate(now);                 // asking for a new one kills the old one
        await sharing.AddGuestLinkAsync(link, current, ct);
        return GuestLinkView.From(link);
    }
}

public sealed class GetGuestLinkUseCase(IPlaceRepository places, ISharingRepository sharing) : IGetGuestLinkUseCase
{
    public async Task<GuestLinkView?> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var link = await sharing.GetActiveGuestLinkAsync(place.Id, ct);
        return link is null ? null : GuestLinkView.From(link);
    }
}

public sealed class DeactivateGuestLinkUseCase(IPlaceRepository places, ISharingRepository sharing, TimeProvider clock)
    : IDeactivateGuestLinkUseCase
{
    public async Task ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var link = await sharing.GetActiveGuestLinkAsync(place.Id, ct);
        if (link is null) return;   // already off: turning it off twice is not an error

        link.Deactivate(clock.GetUtcNow().UtcDateTime);
        await sharing.UpdateGuestLinkAsync(link, ct);
    }
}

/// <summary>
/// HU-040. Unknown token, deactivated link and deleted place all answer the same, so a token cannot be probed.
/// </summary>
public sealed class ResolveGuestLinkUseCase(
    IPlaceRepository places, ISharingRepository sharing, IGeographyReader geography) : IResolveGuestLinkUseCase
{
    public async Task<GuestPlaceView> ExecuteAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < GuestLink.MinTokenLength || token.Length > 64)
            throw new GuestLinkNotFoundException();

        var link = await sharing.FindActiveGuestLinkByTokenAsync(token, ct)
                   ?? throw new GuestLinkNotFoundException();
        var place = await places.GetActiveByIdAsync(link.PlaceId, ct)
                    ?? throw new GuestLinkNotFoundException();
        var city = await geography.GetCityAsync(place.CityId, ct)
                   ?? throw new CityNotFoundException(place.CityId);

        return new GuestPlaceView(place.Id, place.Name, city.CityName, place.MeasurementUnit);
    }
}
