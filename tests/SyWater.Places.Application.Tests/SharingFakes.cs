using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Application.Tests;

/// <summary>In-memory adapter of guest links, invitations and collaborators. Instances are shared, like a tracked ORM.</summary>
internal sealed class InMemorySharingRepository : ISharingRepository
{
    public List<GuestLink> Links { get; } = [];
    public List<Invitation> Invitations { get; } = [];
    public List<Member> Members { get; } = [];
    public List<PlaceActivity> Activity { get; } = [];

    public Task<GuestLink?> GetActiveGuestLinkAsync(Guid placeId, CancellationToken ct) =>
        Task.FromResult(Links.FirstOrDefault(l => l.PlaceId == placeId && l.IsActive));

    public Task<GuestLink?> FindActiveGuestLinkByTokenAsync(string token, CancellationToken ct) =>
        Task.FromResult(Links.FirstOrDefault(l => l.Token == token && l.IsActive));

    public Task AddGuestLinkAsync(GuestLink link, GuestLink? replaced, CancellationToken ct)
    {
        Links.Add(link);
        return Task.CompletedTask;
    }

    public Task UpdateGuestLinkAsync(GuestLink link, CancellationToken ct) => Task.CompletedTask;

    public Task<Invitation?> GetInvitationAsync(Guid invitationId, CancellationToken ct) =>
        Task.FromResult(Invitations.FirstOrDefault(i => i.Id == invitationId));

    public Task<Invitation?> FindPendingInvitationAsync(Guid placeId, string email, CancellationToken ct) =>
        Task.FromResult(Invitations.FirstOrDefault(i =>
            i.PlaceId == placeId && i.Email == email && i.Status == InvitationStatus.Pending));

    public Task<IReadOnlyList<Invitation>> ListInvitationsAsync(Guid placeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Invitation>>(Invitations.Where(i => i.PlaceId == placeId).ToList());

    public Task<IReadOnlyList<Invitation>> ListPendingInvitationsForEmailAsync(string email, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Invitation>>(
            Invitations.Where(i => i.Email == email && i.Status == InvitationStatus.Pending).ToList());

    public Task AddInvitationAsync(Invitation invitation, CancellationToken ct)
    {
        // The filtered unique index of the database: one PENDING per place and e-mail
        if (Invitations.Any(i => i.PlaceId == invitation.PlaceId && i.Email == invitation.Email && i.Status == InvitationStatus.Pending))
            throw new InvalidOperationException("UX_inv_pending violated");
        Invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task UpdateInvitationAsync(Invitation invitation, CancellationToken ct) => Task.CompletedTask;

    public Task<Member?> FindMemberAsync(Guid placeId, Guid userId, CancellationToken ct) =>
        Task.FromResult(Members.FirstOrDefault(m => m.PlaceId == placeId && m.UserId == userId));

    public Task<Member?> GetActiveMemberAsync(Guid placeId, Guid memberId, CancellationToken ct) =>
        Task.FromResult(Members.FirstOrDefault(m => m.PlaceId == placeId && m.Id == memberId && m.IsActive));

    public Task<IReadOnlyList<Member>> ListActiveMembersAsync(Guid placeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Member>>(Members.Where(m => m.PlaceId == placeId && m.IsActive).ToList());

    public Task<IReadOnlyList<Member>> ListActiveMembershipsAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Member>>(Members.Where(m => m.UserId == userId && m.IsActive).ToList());

    public Task UpdateMemberAsync(Member member, CancellationToken ct) => Task.CompletedTask;

    public Task AcceptInvitationAsync(Invitation accepted, Member member, bool memberIsNew, CancellationToken ct)
    {
        if (memberIsNew) Members.Add(member);
        return Task.CompletedTask;
    }

    public Task RevokeMemberAsync(Member revoked, PlaceActivity activity, CancellationToken ct)
    {
        Activity.Add(activity);
        return Task.CompletedTask;
    }
}

/// <summary>ms-iam double: a few registered people, or pretends to be down.</summary>
internal sealed class FakeUserDirectory : IUserDirectory
{
    public Dictionary<Guid, UserContact> Users { get; } = [];
    public bool IsDown { get; set; }

    public UserContact Register(string email, string name)
    {
        var contact = new UserContact(Guid.NewGuid(), email, name);
        Users[contact.UserId] = contact;
        return contact;
    }

    public Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        IsDown
            ? throw new ExternalServiceUnavailableException("iam")
            : Task.FromResult<IReadOnlyDictionary<Guid, UserContact>>(
                userIds.Where(Users.ContainsKey).ToDictionary(id => id, id => Users[id]));

    public Task<UserContact?> FindByEmailAsync(string email, CancellationToken ct) =>
        IsDown
            ? throw new ExternalServiceUnavailableException("iam")
            : Task.FromResult(Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
}

internal sealed class FakeInvitationNotifier : IInvitationNotifier
{
    public List<InvitationMail> Sent { get; } = [];
    public bool Fails { get; set; }

    public Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken ct)
    {
        if (Fails) return Task.FromResult(false);
        Sent.Add(mail);
        return Task.FromResult(true);
    }
}

internal sealed class FakeTokenGenerator : IGuestTokenGenerator
{
    private int _n;
    public string NewToken() => $"token-{++_n:D3}-".PadRight(40, 'x');
}
