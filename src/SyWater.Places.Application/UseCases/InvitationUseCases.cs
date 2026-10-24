using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.UseCases;

/// <summary>HU-042. Only the owner invites.</summary>
public sealed class InviteCollaboratorUseCase(
    IPlaceRepository places,
    ISharingRepository sharing,
    IUserDirectory users,
    IInvitationNotifier notifier,
    TimeProvider clock,
    IInvitationAlerts? alerts = null) : IInviteCollaboratorUseCase
{
    public async Task<SentInvitationView> ExecuteAsync(InviteCollaboratorCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);

        var now = clock.GetUtcNow().UtcDateTime;
        var permissions = PermissionSet.FromKeys(command.Permissions);
        var invitation = Invitation.Create(place.Id, command.OwnerId, command.Email, permissions, now);   // validates the e-mail

        var contacts = await users.GetContactsAsync([command.OwnerId], ct);
        contacts.TryGetValue(command.OwnerId, out var owner);
        if (owner is not null && invitation.IsFor(owner.Email))
            throw new InvalidInvitationException("You cannot invite yourself.");

        // INV-I002: already collaborating
        var registered = await users.FindByEmailAsync(invitation.Email, ct);
        if (registered is not null)
        {
            if (registered.UserId == command.OwnerId)
                throw new InvalidInvitationException("You cannot invite yourself.");
            var existing = await sharing.FindMemberAsync(place.Id, registered.UserId, ct);
            if (existing is { IsActive: true }) throw new AlreadyMemberException(invitation.Email);
        }

        // INV-I003: one pending invitation per e-mail. One that is past its date is closed so a new one can be sent.
        var pending = await sharing.FindPendingInvitationAsync(place.Id, invitation.Email, ct);
        if (pending is not null)
        {
            if (pending.StatusAt(now) == InvitationStatus.Pending)
                throw new InvitationAlreadyPendingException(invitation.Email);
            pending.Expire(now);
            await sharing.UpdateInvitationAsync(pending, ct);
        }

        await sharing.AddInvitationAsync(invitation, ct);

        var sent = await notifier.SendInvitationAsync(
            new InvitationMail(invitation.Id, invitation.Email, place.Name, owner?.Name, invitation.Permissions.Keys, invitation.ExpiresAt), ct);

        // The notification center (and WhatsApp / SMS) of the invited person, when the e-mail already has an account
        if (alerts is not null)
            await alerts.InvitationSentAsync(new InvitationAlert(invitation.Id, place.Id, place.Name, registered?.UserId, owner?.Name,
                invitation.Permissions.Keys, invitation.ExpiresAt), ct);
        return new SentInvitationView(InvitationView.From(invitation, now), sent);
    }
}

public sealed class ListInvitationsUseCase(IPlaceRepository places, ISharingRepository sharing, TimeProvider clock)
    : IListInvitationsUseCase
{
    public async Task<IReadOnlyList<InvitationView>> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var now = clock.GetUtcNow().UtcDateTime;
        return (await sharing.ListInvitationsAsync(place.Id, ct))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => InvitationView.From(i, now))
            .ToList();
    }
}

public sealed class CancelInvitationUseCase(IPlaceRepository places, ISharingRepository sharing, TimeProvider clock)
    : ICancelInvitationUseCase
{
    public async Task ExecuteAsync(Guid ownerId, Guid placeId, Guid invitationId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);
        var invitation = await sharing.GetInvitationAsync(invitationId, ct);
        if (invitation is null || invitation.PlaceId != place.Id) throw new InvitationNotFoundException(invitationId);

        invitation.Reject(clock.GetUtcNow().UtcDateTime);   // the owner withdraws it: it is closed like a rejected one
        await sharing.UpdateInvitationAsync(invitation, ct);
    }
}

/// <summary>HU-043: the inbox of the user, matched by the e-mail of their account.</summary>
public sealed class ListMyInvitationsUseCase(
    IPlaceRepository places, ISharingRepository sharing, IUserDirectory users, TimeProvider clock)
    : IListMyInvitationsUseCase
{
    public async Task<IReadOnlyList<InboxInvitationView>> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        var me = (await users.GetContactsAsync([userId], ct)).GetValueOrDefault(userId);
        if (me is null) return [];

        var now = clock.GetUtcNow().UtcDateTime;
        var open = (await sharing.ListPendingInvitationsForEmailAsync(me.Email.Trim().ToLowerInvariant(), ct))
            .Where(i => i.StatusAt(now) == InvitationStatus.Pending)
            .ToList();
        if (open.Count == 0) return [];

        var found = (await places.ListActiveByIdsAsync(open.Select(i => i.PlaceId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var inviters = await users.GetContactsAsync(open.Select(i => i.InvitedBy).Distinct().ToList(), ct);

        return open
            .Where(i => found.ContainsKey(i.PlaceId))   // the owner deleted the place: nothing to accept
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InboxInvitationView(
                i.Id, i.PlaceId, found[i.PlaceId].Name,
                inviters.GetValueOrDefault(i.InvitedBy)?.Name, i.Permissions.Keys, i.ExpiresAt))
            .ToList();
    }
}

/// <summary>HU-043: accept or reject. The invitation only works for the account whose e-mail it was sent to.</summary>
public sealed class AnswerInvitationUseCase(
    IPlaceRepository places,
    ISharingRepository sharing,
    IUserDirectory users,
    IGeographyReader geography,
    TimeProvider clock,
    IInvitationAlerts? alerts = null) : IAnswerInvitationUseCase
{
    public async Task<SharedPlaceView> AcceptAsync(Guid userId, Guid invitationId, CancellationToken ct)
    {
        var (invitation, place, me) = await LoadAsync(userId, invitationId, ct);
        if (place.OwnerId == userId) throw new InvitationNotFoundException(invitationId);

        var now = clock.GetUtcNow().UtcDateTime;
        invitation.Accept(now);   // 409 if expired or already answered

        var member = await sharing.FindMemberAsync(place.Id, userId, ct);
        var isNew = member is null;
        if (member is null) member = Member.Create(place.Id, userId, invitation.Permissions, now);
        else member.Rejoin(invitation.Permissions, now);

        await sharing.AcceptInvitationAsync(invitation, member, isNew, ct);
        await TellOwnerAsync(invitation, place, me, accepted: true, ct);

        var city = await geography.GetCityAsync(place.CityId, ct) ?? throw new CityNotFoundException(place.CityId);
        return new SharedPlaceView(PlaceView.From(place, city), "COLLABORATOR", member.Permissions.Keys, member.Id);
    }

    public async Task RejectAsync(Guid userId, Guid invitationId, CancellationToken ct)
    {
        var (invitation, place, me) = await LoadAsync(userId, invitationId, ct);
        invitation.Reject(clock.GetUtcNow().UtcDateTime);
        await sharing.UpdateInvitationAsync(invitation, ct);
        await TellOwnerAsync(invitation, place, me, accepted: false, ct);
    }

    // The owner learns the answer in the app (best effort: the answer is already saved)
    private async Task TellOwnerAsync(Invitation invitation, Place place, UserContact me, bool accepted, CancellationToken ct)
    {
        if (alerts is null) return;
        await alerts.InvitationAnsweredAsync(
            new InvitationAnswerAlert(invitation.Id, place.Id, place.Name, place.OwnerId, me.Name, accepted), ct);
    }

    // Anything that is not "an invitation sent to my e-mail for a place that still exists" is the same 404
    private async Task<(Invitation, Place, UserContact)> LoadAsync(Guid userId, Guid invitationId, CancellationToken ct)
    {
        var invitation = await sharing.GetInvitationAsync(invitationId, ct)
                         ?? throw new InvitationNotFoundException(invitationId);

        var me = (await users.GetContactsAsync([userId], ct)).GetValueOrDefault(userId);
        if (me is null || !invitation.IsFor(me.Email)) throw new InvitationNotFoundException(invitationId);

        var place = await places.GetActiveByIdAsync(invitation.PlaceId, ct)
                    ?? throw new InvitationNotFoundException(invitationId);
        return (invitation, place, me);
    }
}
