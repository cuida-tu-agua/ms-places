using System.Text.Json;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.UseCases;

public sealed class ListMembersUseCase(IPlaceRepository places, ISharingRepository sharing, IUserDirectory users)
    : IListMembersUseCase
{
    public async Task<IReadOnlyList<MemberView>> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var members = await sharing.ListActiveMembersAsync(place.Id, ct);
        if (members.Count == 0) return [];

        // Names and e-mails are a courtesy: if ms-iam is down the list still shows who has access
        IReadOnlyDictionary<Guid, UserContact> contacts;
        try { contacts = await users.GetContactsAsync(members.Select(m => m.UserId).ToList(), ct); }
        catch (ExternalServiceUnavailableException) { contacts = new Dictionary<Guid, UserContact>(); }

        return members
            .OrderBy(m => m.CreatedAt)
            .Select(m => MemberView.From(m, contacts.GetValueOrDefault(m.UserId)))
            .ToList();
    }
}

/// <summary>HU-044.</summary>
public sealed class ChangeMemberPermissionsUseCase(
    IPlaceRepository places, ISharingRepository sharing, IUserDirectory users, TimeProvider clock)
    : IChangeMemberPermissionsUseCase
{
    public async Task<MemberView> ExecuteAsync(ChangePermissionsCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);
        var member = await sharing.GetActiveMemberAsync(place.Id, command.MemberId, ct)
                     ?? throw new MemberNotFoundException(command.MemberId);

        member.ChangePermissions(PermissionSet.FromKeys(command.Permissions), clock.GetUtcNow().UtcDateTime);
        await sharing.UpdateMemberAsync(member, ct);

        UserContact? contact = null;
        try { contact = (await users.GetContactsAsync([member.UserId], ct)).GetValueOrDefault(member.UserId); }
        catch (ExternalServiceUnavailableException) { /* the change is saved; the name is only decoration */ }
        return MemberView.From(member, contact);
    }
}

/// <summary>HU-046.</summary>
public sealed class RevokeMemberUseCase(IPlaceRepository places, ISharingRepository sharing, TimeProvider clock)
    : IRevokeMemberUseCase
{
    public async Task ExecuteAsync(Guid ownerId, Guid placeId, Guid memberId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var member = await sharing.GetActiveMemberAsync(place.Id, memberId, ct)
                     ?? throw new MemberNotFoundException(memberId);
        await RevokeAsync(place, member, ownerId, clock, sharing, ct);
    }

    internal static async Task RevokeAsync(
        Place place, Member member, Guid actorId, TimeProvider clock, ISharingRepository sharing, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        member.Revoke(now);
        var activity = new PlaceActivity(
            place.Id, actorId, PlaceActivity.CollaboratorRevoked,
            JsonSerializer.Serialize(new { userId = member.UserId, memberId = member.Id }), now);
        await sharing.RevokeMemberAsync(member, activity, ct);
    }
}

public sealed class LeavePlaceUseCase(IPlaceRepository places, ISharingRepository sharing, TimeProvider clock)
    : ILeavePlaceUseCase
{
    public async Task ExecuteAsync(Guid userId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveByIdAsync(placeId, ct) ?? throw new PlaceNotFoundException(placeId);
        var member = await sharing.FindMemberAsync(place.Id, userId, ct);
        if (member is not { IsActive: true }) throw new PlaceNotFoundException(placeId);
        await RevokeMemberUseCase.RevokeAsync(place, member, userId, clock, sharing, ct);
    }
}
