using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.UseCases;

/// <summary>
/// The one place that decides who may do what in a place. The owner may do everything; a collaborator what the owner
/// gave them; anybody else gets "place not found", the same answer as for a place that does not exist, so nobody can
/// find out which ids are real.
/// </summary>
public sealed class PlaceAccessService(IPlaceRepository places, ISharingRepository sharing) : IPlaceAccessService
{
    public async Task<PlaceAccess> GetAsync(Guid userId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveByIdAsync(placeId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        if (place.OwnerId == userId)
            return new PlaceAccess(place, AccessRole.Owner, PermissionSet.Owner);

        var member = await sharing.FindMemberAsync(placeId, userId, ct);
        if (member is { IsActive: true })
            return new PlaceAccess(place, AccessRole.Collaborator, member.Permissions);

        throw new PlaceNotFoundException(placeId);
    }

    public async Task<PlaceAccess> RequireAsync(Guid userId, Guid placeId, Permission permission, CancellationToken ct)
    {
        var access = await GetAsync(userId, placeId, ct);
        access.Require(permission);
        return access;
    }
}

public sealed class GetPlaceAccessUseCase(IPlaceAccessService access) : IGetPlaceAccessUseCase
{
    public async Task<PlaceAccessView> ExecuteAsync(Guid userId, Guid placeId, CancellationToken ct) =>
        PlaceAccessView.From(await access.GetAsync(userId, placeId, ct));
}

/// <summary>HU-045.</summary>
public sealed class ListSharedPlacesUseCase(
    IPlaceRepository places, ISharingRepository sharing, IGeographyReader geography) : IListSharedPlacesUseCase
{
    public async Task<IReadOnlyList<SharedPlaceView>> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await sharing.ListActiveMembershipsAsync(userId, ct);
        if (memberships.Count == 0) return [];

        var found = await places.ListActiveByIdsAsync(memberships.Select(m => m.PlaceId).Distinct().ToList(), ct);
        if (found.Count == 0) return [];

        var cities = await geography.GetCitiesAsync(found.Select(p => p.CityId).Distinct().ToList(), ct);
        var byId = found.ToDictionary(p => p.Id);

        var result = new List<SharedPlaceView>();
        foreach (var member in memberships)
        {
            // A place deleted by its owner is not in `found`: the membership just stops showing
            if (!byId.TryGetValue(member.PlaceId, out var place)) continue;
            if (!cities.TryGetValue(place.CityId, out var city))
                throw new InvalidOperationException($"City {place.CityId} of place {place.Id} is not in the catalog.");
            result.Add(new SharedPlaceView(PlaceView.From(place, city), "COLLABORATOR", member.Permissions.Keys, member.Id));
        }
        return result.OrderBy(v => v.Place.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

/// <summary>HU-047: for the notifications service.</summary>
public sealed class GetPlaceRecipientsUseCase(IPlaceRepository places, ISharingRepository sharing) : IGetPlaceRecipientsUseCase
{
    public async Task<IReadOnlyList<PlaceRecipientView>> ExecuteAsync(Guid placeId, Permission permission, CancellationToken ct)
    {
        var place = await places.GetActiveByIdAsync(placeId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var recipients = new List<PlaceRecipientView> { new(place.OwnerId, "OWNER") };
        foreach (var member in await sharing.ListActiveMembersAsync(placeId, ct))
            if (member.Permissions.Has(permission))
                recipients.Add(new PlaceRecipientView(member.UserId, "COLLABORATOR"));
        return recipients;
    }
}
