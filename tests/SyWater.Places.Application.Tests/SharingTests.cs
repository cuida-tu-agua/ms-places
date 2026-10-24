using SyWater.Places.Application.Places;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.Tests;

/// <summary>HU-039 to HU-046: guest link, invitations, collaborators and their permissions.</summary>
public class SharingTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly InMemorySharingRepository _sharing = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeInvitationNotifier _mail = new();
    private readonly FakeInvitationAlerts _alerts = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 9, 15, 0, 0, TimeSpan.Zero));
    private readonly UserContact _owner;
    private readonly UserContact _ana;
    private readonly UserContact _luis;
    private readonly Guid _placeId;

    public SharingTests()
    {
        _owner = _users.Register("owner@mail.com", "Olga Owner");
        _ana = _users.Register("ana@mail.com", "Ana");
        _luis = _users.Register("luis@mail.com", "Luis");
        var created = new CreatePlaceUseCase(_places, _geography, _clock).ExecuteAsync(
            new CreatePlaceCommand(_owner.UserId, FakeGeographyReader.Bogota.CityId, "Casa", PlaceType.Residential,
                "Calle 1", null), CancellationToken.None).GetAwaiter().GetResult();
        _placeId = created.Id;
    }

    private PlaceAccessService Access => new(_places, _sharing);
    private readonly FakeTokenGenerator _tokens = new();
    private CreateGuestLinkUseCase CreateLink => new(_places, _sharing, _tokens, _clock);
    private ResolveGuestLinkUseCase ResolveLink => new(_places, _sharing, _geography);
    private InviteCollaboratorUseCase Invite => new(_places, _sharing, _users, _mail, _clock, _alerts);
    private AnswerInvitationUseCase Answer => new(_places, _sharing, _users, _geography, _clock, _alerts);

    private Task<SentInvitationView> InviteAna(params string[] permissions) =>
        Invite.ExecuteAsync(new InviteCollaboratorCommand(_owner.UserId, _placeId, "ana@mail.com", permissions), default);

    private async Task<SharedPlaceView> AnaCollaborates(params string[] permissions)
    {
        var sent = await InviteAna(permissions);
        return await Answer.AcceptAsync(_ana.UserId, sent.Invitation.Id, default);
    }

    // ── Guest link: HU-039 / HU-040 / HU-041 ─────────────────────────────

    [Fact]
    public async Task The_owner_creates_the_link_and_asking_again_returns_the_same_one()
    {
        var first = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);
        var second = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);

        Assert.Equal(first.Token, second.Token);
        Assert.Single(_sharing.Links);
        Assert.True(first.Token.Length >= GuestLink.MinTokenLength);
    }

    [Fact]
    public async Task Regenerating_kills_the_old_link_and_the_new_one_works()
    {
        var old = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);
        var fresh = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, true, default);

        Assert.NotEqual(old.Token, fresh.Token);
        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => ResolveLink.ExecuteAsync(old.Token, default));
        Assert.Equal("Casa", (await ResolveLink.ExecuteAsync(fresh.Token, default)).Name);
    }

    [Fact]
    public async Task Only_the_owner_can_create_the_link()
    {
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => CreateLink.ExecuteAsync(_ana.UserId, _placeId, false, default));

        await AnaCollaborates("CONTROL_VALVE");
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => CreateLink.ExecuteAsync(_ana.UserId, _placeId, false, default));
    }

    [Fact]
    public async Task A_guest_sees_the_name_the_city_and_the_unit_of_the_place()
    {
        var link = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);

        var view = await ResolveLink.ExecuteAsync(link.Token, default);

        Assert.Equal(_placeId, view.PlaceId);
        Assert.Equal("Casa", view.Name);
        Assert.Equal(FakeGeographyReader.Bogota.CityName, view.CityName);
    }

    [Fact]
    public async Task A_deactivated_link_stops_working_at_once()
    {
        var link = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);
        await new DeactivateGuestLinkUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, default);

        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => ResolveLink.ExecuteAsync(link.Token, default));
        // turning it off twice is fine
        await new DeactivateGuestLinkUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, default);
    }

    [Fact]
    public async Task The_link_of_a_deleted_place_stops_working()
    {
        var link = await CreateLink.ExecuteAsync(_owner.UserId, _placeId, false, default);
        _places.Items.Single().Delete(_clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => ResolveLink.ExecuteAsync(link.Token, default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("a-token-that-does-not-exist-but-has-the-right-length-0123456789")]
    public async Task Garbage_and_unknown_tokens_get_the_same_not_found(string token)
    {
        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => ResolveLink.ExecuteAsync(token, default));
    }

    // ── Invitations: HU-042 ──────────────────────────────────────────────

    [Fact]
    public async Task The_owner_invites_by_email_and_the_mail_goes_out()
    {
        var sent = await Invite.ExecuteAsync(
            new InviteCollaboratorCommand(_owner.UserId, _placeId, "  Ana@Mail.com ", ["CONTROL_VALVE"]), default);

        Assert.True(sent.EmailSent);
        Assert.Equal("ana@mail.com", sent.Invitation.Email);
        Assert.Equal("PENDING", sent.Invitation.Status);
        Assert.Equal(["VIEW_CONSUMPTION", "CONTROL_VALVE"], sent.Invitation.Permissions);
        var mail = Assert.Single(_mail.Sent);
        Assert.Equal("ana@mail.com", mail.ToEmail);
        Assert.Equal("Casa", mail.PlaceName);
        Assert.Equal("Olga Owner", mail.InvitedByName);
        Assert.Equal(sent.Invitation.ExpiresAt, mail.ExpiresAt);
        Assert.Equal(TimeSpan.FromDays(7), sent.Invitation.ExpiresAt - sent.Invitation.CreatedAt);
    }

    [Fact]
    public async Task A_failed_mail_does_not_lose_the_invitation()
    {
        _mail.Fails = true;

        var sent = await InviteAna();

        Assert.False(sent.EmailSent);
        Assert.Single(_sharing.Invitations);
    }

    [Fact]
    public async Task You_can_invite_someone_who_has_no_account_yet()
    {
        var sent = await Invite.ExecuteAsync(
            new InviteCollaboratorCommand(_owner.UserId, _placeId, "newbie@mail.com", []), default);

        Assert.Equal(["VIEW_CONSUMPTION"], sent.Invitation.Permissions);   // consumption is always included
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public async Task A_bad_email_is_rejected(string email)
    {
        await Assert.ThrowsAsync<InvalidInvitationException>(() => Invite.ExecuteAsync(
            new InviteCollaboratorCommand(_owner.UserId, _placeId, email, []), default));
        Assert.Empty(_sharing.Invitations);
    }

    [Fact]
    public async Task An_unknown_permission_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidPermissionsException>(() => InviteAna("FLY_THE_DRONE"));
    }

    [Fact]
    public async Task The_owner_cannot_invite_themselves()
    {
        await Assert.ThrowsAsync<InvalidInvitationException>(() => Invite.ExecuteAsync(
            new InviteCollaboratorCommand(_owner.UserId, _placeId, "OWNER@mail.com", []), default));
    }

    [Fact]
    public async Task Only_the_owner_can_invite()
    {
        await AnaCollaborates();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Invite.ExecuteAsync(
            new InviteCollaboratorCommand(_ana.UserId, _placeId, "luis@mail.com", []), default));
    }

    [Fact]
    public async Task A_second_pending_invitation_for_the_same_email_is_a_conflict()
    {
        await InviteAna();

        await Assert.ThrowsAsync<InvitationAlreadyPendingException>(() => InviteAna());
    }

    [Fact]
    public async Task Someone_who_already_collaborates_cannot_be_invited_again()
    {
        await AnaCollaborates();

        await Assert.ThrowsAsync<AlreadyMemberException>(() => InviteAna());
    }

    [Fact]
    public async Task An_expired_invitation_does_not_block_a_new_one()
    {
        var first = await InviteAna();
        _clock.Advance(TimeSpan.FromDays(8));

        var second = await InviteAna("VIEW_ALERTS");

        Assert.NotEqual(first.Invitation.Id, second.Invitation.Id);
        Assert.Equal(InvitationStatus.Expired, _sharing.Invitations.First(i => i.Id == first.Invitation.Id).Status);
    }

    [Fact]
    public async Task The_owner_lists_invitations_with_expired_ones_marked_as_such()
    {
        await InviteAna();
        _clock.Advance(TimeSpan.FromDays(8));

        var list = await new ListInvitationsUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, default);

        Assert.Equal("EXPIRED", Assert.Single(list).Status);
    }

    [Fact]
    public async Task The_owner_can_withdraw_a_pending_invitation_and_invite_again()
    {
        var sent = await InviteAna();

        await new CancelInvitationUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, sent.Invitation.Id, default);

        await Assert.ThrowsAsync<InvitationNotPendingException>(() => Answer.AcceptAsync(_ana.UserId, sent.Invitation.Id, default));
        await InviteAna();   // no conflict now
    }

    // ── Invitations: HU-043 ──────────────────────────────────────────────

    [Fact]
    public async Task The_invited_person_sees_the_invitation_in_their_inbox()
    {
        await InviteAna("VIEW_ALERTS");

        var inbox = await new ListMyInvitationsUseCase(_places, _sharing, _users, _clock).ExecuteAsync(_ana.UserId, default);

        var item = Assert.Single(inbox);
        Assert.Equal("Casa", item.PlaceName);
        Assert.Equal("Olga Owner", item.InvitedByName);
        Assert.Equal(["VIEW_CONSUMPTION", "VIEW_ALERTS"], item.Permissions);
        Assert.Empty(await new ListMyInvitationsUseCase(_places, _sharing, _users, _clock).ExecuteAsync(_luis.UserId, default));
    }

    [Fact]
    public async Task Accepting_makes_the_person_a_collaborator_with_the_permissions_of_the_invitation()
    {
        var shared = await AnaCollaborates("CONTROL_VALVE", "VIEW_BILLS");

        Assert.Equal("Casa", shared.Place.Name);
        Assert.Equal(["VIEW_CONSUMPTION", "CONTROL_VALVE", "VIEW_BILLS"], shared.Permissions);
        var access = await Access.GetAsync(_ana.UserId, _placeId, default);
        Assert.Equal(AccessRole.Collaborator, access.Role);
        Assert.True(access.Permissions.Has(Permission.ControlValve));
        Assert.False(access.Permissions.Has(Permission.ViewAlerts));
        Assert.Equal(InvitationStatus.Accepted, _sharing.Invitations.Single().Status);
    }

    [Fact]
    public async Task An_invitation_only_works_for_the_account_it_was_sent_to()
    {
        var sent = await InviteAna();

        await Assert.ThrowsAsync<InvitationNotFoundException>(() => Answer.AcceptAsync(_luis.UserId, sent.Invitation.Id, default));
        await Assert.ThrowsAsync<InvitationNotFoundException>(() => Answer.RejectAsync(_luis.UserId, sent.Invitation.Id, default));
        Assert.Empty(_sharing.Members);
    }

    [Fact]
    public async Task An_expired_invitation_cannot_be_accepted()
    {
        var sent = await InviteAna();
        _clock.Advance(TimeSpan.FromDays(7));

        await Assert.ThrowsAsync<InvitationExpiredException>(() => Answer.AcceptAsync(_ana.UserId, sent.Invitation.Id, default));
        Assert.Empty(_sharing.Members);
    }

    [Fact]
    public async Task An_invitation_can_be_answered_only_once()
    {
        var sent = await InviteAna();
        await Answer.RejectAsync(_ana.UserId, sent.Invitation.Id, default);

        await Assert.ThrowsAsync<InvitationNotPendingException>(() => Answer.AcceptAsync(_ana.UserId, sent.Invitation.Id, default));
        Assert.Empty(_sharing.Members);
        Assert.Empty(await new ListMyInvitationsUseCase(_places, _sharing, _users, _clock).ExecuteAsync(_ana.UserId, default));
    }

    [Fact]
    public async Task An_invitation_to_a_deleted_place_cannot_be_accepted()
    {
        var sent = await InviteAna();
        _places.Items.Single().Delete(_clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<InvitationNotFoundException>(() => Answer.AcceptAsync(_ana.UserId, sent.Invitation.Id, default));
    }

    // ── Access ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_owner_has_every_permission()
    {
        var access = await Access.GetAsync(_owner.UserId, _placeId, default);

        Assert.Equal(AccessRole.Owner, access.Role);
        foreach (var permission in Enum.GetValues<Permission>()) Assert.True(access.Permissions.Has(permission));
    }

    [Fact]
    public async Task A_stranger_gets_not_found_never_forbidden()
    {
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Access.GetAsync(_luis.UserId, _placeId, default));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Access.GetAsync(_luis.UserId, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task A_collaborator_without_the_permission_gets_forbidden()
    {
        await AnaCollaborates();   // consumption only

        await Access.RequireAsync(_ana.UserId, _placeId, Permission.ViewConsumption, default);
        var ex = await Assert.ThrowsAsync<PermissionDeniedException>(
            () => Access.RequireAsync(_ana.UserId, _placeId, Permission.ControlValve, default));
        Assert.Equal("permission.denied", ex.Code);
    }

    [Fact]
    public async Task The_access_view_tells_other_services_the_role_and_the_permissions()
    {
        await AnaCollaborates("VIEW_DEVICE_STATUS");

        var view = await new GetPlaceAccessUseCase(Access).ExecuteAsync(_ana.UserId, _placeId, default);

        Assert.Equal("COLLABORATOR", view.Role);
        Assert.Equal(["VIEW_CONSUMPTION", "VIEW_DEVICE_STATUS"], view.Permissions);
        Assert.Equal("OWNER", (await new GetPlaceAccessUseCase(Access).ExecuteAsync(_owner.UserId, _placeId, default)).Role);
    }

    [Fact]
    public async Task A_collaborator_sees_the_place_among_the_shared_ones_and_not_among_their_own()
    {
        await AnaCollaborates("VIEW_ALERTS");

        var shared = await new ListSharedPlacesUseCase(_places, _sharing, _geography).ExecuteAsync(_ana.UserId, default);
        var own = await new ListPlacesUseCase(_places, _geography).ExecuteAsync(_ana.UserId, default);

        var item = Assert.Single(shared);
        Assert.Equal(_placeId, item.Place.Id);
        Assert.Equal(["VIEW_CONSUMPTION", "VIEW_ALERTS"], item.Permissions);
        Assert.Empty(own);
        Assert.Empty(await new ListSharedPlacesUseCase(_places, _sharing, _geography).ExecuteAsync(_owner.UserId, default));
    }

    [Fact]
    public async Task A_place_the_owner_deleted_disappears_from_the_shared_list()
    {
        await AnaCollaborates();
        _places.Items.Single().Delete(_clock.GetUtcNow().UtcDateTime);

        Assert.Empty(await new ListSharedPlacesUseCase(_places, _sharing, _geography).ExecuteAsync(_ana.UserId, default));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Access.GetAsync(_ana.UserId, _placeId, default));
    }

    // ── Collaborators: HU-044 / HU-046 ───────────────────────────────────

    [Fact]
    public async Task The_owner_lists_the_collaborators_with_their_names()
    {
        await AnaCollaborates("CONTROL_VALVE");

        var list = await new ListMembersUseCase(_places, _sharing, _users).ExecuteAsync(_owner.UserId, _placeId, default);

        var member = Assert.Single(list);
        Assert.Equal("Ana", member.Name);
        Assert.Equal("ana@mail.com", member.Email);
    }

    [Fact]
    public async Task The_list_still_works_when_ms_iam_is_down()
    {
        await AnaCollaborates();
        _users.IsDown = true;

        var list = await new ListMembersUseCase(_places, _sharing, _users).ExecuteAsync(_owner.UserId, _placeId, default);

        var member = Assert.Single(list);
        Assert.Null(member.Name);
        Assert.Equal(_ana.UserId, member.UserId);
    }

    [Fact]
    public async Task Changing_permissions_applies_at_once()
    {
        var shared = await AnaCollaborates("CONTROL_VALVE");
        await Access.RequireAsync(_ana.UserId, _placeId, Permission.ControlValve, default);

        var changed = await new ChangeMemberPermissionsUseCase(_places, _sharing, _users, _clock).ExecuteAsync(
            new ChangePermissionsCommand(_owner.UserId, _placeId, shared.MemberId, ["VIEW_ALERTS"]), default);

        Assert.Equal(["VIEW_CONSUMPTION", "VIEW_ALERTS"], changed.Permissions);
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => Access.RequireAsync(_ana.UserId, _placeId, Permission.ControlValve, default));
        await Access.RequireAsync(_ana.UserId, _placeId, Permission.ViewAlerts, default);
    }

    [Fact]
    public async Task A_collaborator_cannot_change_permissions_not_even_their_own()
    {
        var shared = await AnaCollaborates();

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => new ChangeMemberPermissionsUseCase(_places, _sharing, _users, _clock)
            .ExecuteAsync(new ChangePermissionsCommand(_ana.UserId, _placeId, shared.MemberId, ["CONTROL_VALVE"]), default));
    }

    [Fact]
    public async Task Revoking_cuts_the_access_at_once_and_leaves_a_trace()
    {
        var shared = await AnaCollaborates("CONTROL_VALVE");

        await new RevokeMemberUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, shared.MemberId, default);

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Access.GetAsync(_ana.UserId, _placeId, default));
        Assert.Empty(await new ListSharedPlacesUseCase(_places, _sharing, _geography).ExecuteAsync(_ana.UserId, default));
        var trace = Assert.Single(_sharing.Activity);
        Assert.Equal(PlaceActivity.CollaboratorRevoked, trace.Action);
        Assert.Equal(_owner.UserId, trace.OwnerId);
    }

    [Fact]
    public async Task Revoking_twice_or_a_stranger_is_not_found()
    {
        var shared = await AnaCollaborates();
        var revoke = new RevokeMemberUseCase(_places, _sharing, _clock);
        await revoke.ExecuteAsync(_owner.UserId, _placeId, shared.MemberId, default);

        await Assert.ThrowsAsync<MemberNotFoundException>(() => revoke.ExecuteAsync(_owner.UserId, _placeId, shared.MemberId, default));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => revoke.ExecuteAsync(_luis.UserId, _placeId, shared.MemberId, default));
    }

    [Fact]
    public async Task A_revoked_collaborator_can_be_invited_again_and_gets_the_new_permissions()
    {
        var shared = await AnaCollaborates("CONTROL_VALVE");
        await new RevokeMemberUseCase(_places, _sharing, _clock).ExecuteAsync(_owner.UserId, _placeId, shared.MemberId, default);

        var again = await AnaCollaborates("VIEW_BILLS");

        Assert.Equal(shared.MemberId, again.MemberId);   // the same row comes back
        Assert.Single(_sharing.Members);
        Assert.Equal(["VIEW_CONSUMPTION", "VIEW_BILLS"], again.Permissions);
        await Access.RequireAsync(_ana.UserId, _placeId, Permission.ViewBills, default);
    }

    [Fact]
    public async Task A_collaborator_can_leave_the_place()
    {
        await AnaCollaborates();

        await new LeavePlaceUseCase(_places, _sharing, _clock).ExecuteAsync(_ana.UserId, _placeId, default);

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Access.GetAsync(_ana.UserId, _placeId, default));
        await Assert.ThrowsAsync<PlaceNotFoundException>(
            () => new LeavePlaceUseCase(_places, _sharing, _clock).ExecuteAsync(_luis.UserId, _placeId, default));
    }

    // ── HU-047: who to tell ──────────────────────────────────────────────

    [Fact]
    public async Task Recipients_are_the_owner_and_the_collaborators_with_the_permission()
    {
        await AnaCollaborates("VIEW_ALERTS");
        var invitation = await Invite.ExecuteAsync(new InviteCollaboratorCommand(_owner.UserId, _placeId, "luis@mail.com", []), default);
        await Answer.AcceptAsync(_luis.UserId, invitation.Invitation.Id, default);   // Luis: consumption only

        var recipients = await new GetPlaceRecipientsUseCase(_places, _sharing).ExecuteAsync(_placeId, Permission.ViewAlerts, default);

        Assert.Equal([_owner.UserId, _ana.UserId], recipients.Select(r => r.UserId).ToArray());
    }

    // ── VIEW_BILLS in the tariff readers ─────────────────────────────────

    [Fact]
    public async Task Tariffs_are_visible_to_a_collaborator_only_with_VIEW_BILLS()
    {
        var tariffs = new InMemoryPlaceTariffRepository();
        var reader = new GetPlaceTariffsUseCase(Access, tariffs, new FakeTariffCatalog(), _clock);

        await AnaCollaborates();   // no VIEW_BILLS
        await Assert.ThrowsAsync<PermissionDeniedException>(() => reader.ExecuteAsync(_ana.UserId, _placeId, default));
        await reader.ExecuteAsync(_owner.UserId, _placeId, default);   // the owner always can

        var member = _sharing.Members.Single();
        member.ChangePermissions(PermissionSet.FromKeys(["VIEW_BILLS"]), _clock.GetUtcNow().UtcDateTime);
        await reader.ExecuteAsync(_ana.UserId, _placeId, default);
    }

    // ── Notices to ms-notification: notification center, WhatsApp / SMS and the answer (HU-042 / HU-043) ──

    [Fact]
    public async Task A_registered_person_is_announced_to_ms_notification_with_their_account()
    {
        var sent = await InviteAna("VIEW_CONSUMPTION", "CONTROL_VALVE");

        var alert = Assert.Single(_alerts.Sent);
        Assert.Equal(sent.Invitation.Id, alert.InvitationId);
        Assert.Equal(_placeId, alert.PlaceId);
        Assert.Equal(_ana.UserId, alert.InviteeUserId);
        Assert.Equal("Casa", alert.PlaceName);
        Assert.Equal("Olga Owner", alert.InvitedByName);
        Assert.Contains("CONTROL_VALVE", alert.Permissions);
    }

    [Fact]
    public async Task An_email_without_account_is_announced_without_a_user()
    {
        await Invite.ExecuteAsync(new InviteCollaboratorCommand(_owner.UserId, _placeId, "nueva@mail.com", ["VIEW_CONSUMPTION"]), default);

        Assert.Null(Assert.Single(_alerts.Sent).InviteeUserId);
    }

    [Fact]
    public async Task The_owner_is_told_when_the_invitation_is_accepted_or_rejected()
    {
        await AnaCollaborates("VIEW_CONSUMPTION");
        var accepted = Assert.Single(_alerts.Answers);
        Assert.True(accepted.Accepted);
        Assert.Equal(_owner.UserId, accepted.OwnerId);
        Assert.Equal("Ana", accepted.InviteeName);

        var sent = await Invite.ExecuteAsync(new InviteCollaboratorCommand(_owner.UserId, _placeId, "luis@mail.com", ["VIEW_CONSUMPTION"]), default);
        await Answer.RejectAsync(_luis.UserId, sent.Invitation.Id, default);

        Assert.False(_alerts.Answers[1].Accepted);
        Assert.Equal("Luis", _alerts.Answers[1].InviteeName);
    }

    [Fact]
    public async Task If_ms_notification_is_down_inviting_and_answering_still_work()
    {
        _alerts.IsDown = true;

        var shared = await AnaCollaborates("VIEW_CONSUMPTION");

        Assert.Equal("COLLABORATOR", shared.Role);
        Assert.Empty(_alerts.Sent);
        Assert.Empty(_alerts.Answers);
    }
}
