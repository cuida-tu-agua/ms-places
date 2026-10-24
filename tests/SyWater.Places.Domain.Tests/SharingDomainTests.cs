using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Domain.Tests;

public class SharingDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Consumption_is_always_in_the_set_and_the_order_is_fixed()
    {
        var set = PermissionSet.FromKeys(["view_alerts", "CONTROL_VALVE", "CONTROL_VALVE"]);

        Assert.Equal(["VIEW_CONSUMPTION", "CONTROL_VALVE", "VIEW_ALERTS"], set.Keys);
        Assert.Equal("VIEW_CONSUMPTION,CONTROL_VALVE,VIEW_ALERTS", set.ToStorage());
    }

    [Fact]
    public void The_storage_format_round_trips()
    {
        var set = PermissionSet.FromKeys(["VIEW_BILLS", "VIEW_DEVICE_STATUS"]);

        Assert.Equal(set, PermissionSet.FromStorage(set.ToStorage()));
    }

    [Fact]
    public void An_unknown_permission_is_an_error()
    {
        Assert.Throws<InvalidPermissionsException>(() => PermissionSet.FromKeys(["NOPE"]));
        Assert.Throws<InvalidPermissionsException>(() => PermissionSet.FromKeys([""]));
    }

    [Fact]
    public void The_owner_set_has_everything()
    {
        foreach (var p in Enum.GetValues<Permission>()) Assert.True(PermissionSet.Owner.Has(p));
    }

    [Fact]
    public void An_invitation_lives_seven_days()
    {
        var i = Invitation.Create(Guid.NewGuid(), Guid.NewGuid(), "A@B.co", PermissionSet.FromKeys([]), Now);

        Assert.Equal(InvitationStatus.Pending, i.StatusAt(Now.AddDays(7).AddTicks(-1)));
        Assert.Equal(InvitationStatus.Expired, i.StatusAt(Now.AddDays(7)));
        Assert.True(i.IsFor(" a@b.CO "));
    }

    [Fact]
    public void Expiring_only_works_on_a_pending_invitation_that_is_past_its_date()
    {
        var i = Invitation.Create(Guid.NewGuid(), Guid.NewGuid(), "a@b.co", PermissionSet.FromKeys([]), Now);

        Assert.Throws<InvitationNotPendingException>(() => i.Expire(Now));
        i.Expire(Now.AddDays(8));
        Assert.Equal(InvitationStatus.Expired, i.Status);
    }

    [Fact]
    public void A_revoked_member_cannot_be_changed_or_revoked_again()
    {
        var m = Member.Create(Guid.NewGuid(), Guid.NewGuid(), PermissionSet.FromKeys([]), Now);
        m.Revoke(Now);

        Assert.False(m.IsActive);
        Assert.Throws<MemberNotFoundException>(() => m.Revoke(Now));
        Assert.Throws<MemberNotFoundException>(() => m.ChangePermissions(PermissionSet.Owner, Now));
        m.Rejoin(PermissionSet.FromKeys(["VIEW_BILLS"]), Now);
        Assert.True(m.IsActive);
    }

    [Fact]
    public void A_short_token_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GuestLink.Create(Guid.NewGuid(), Guid.NewGuid(), "short", Now));
        var link = GuestLink.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 43), Now);
        link.Deactivate(Now);
        link.Deactivate(Now.AddDays(1));
        Assert.Equal(Now, link.DeactivatedAt);
    }
}
