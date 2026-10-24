using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Sharing;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;

/// <summary>
/// Outbound adapter for guest links, invitations and collaborators. places_rw has no DELETE: rows change state
/// (deactivated_at, status, revoked_at) and nothing is ever removed. The filtered unique indexes of v1.4 back the rules
/// "one active link per place" and "one pending invitation per place and e-mail" even under concurrent requests.
/// </summary>
public sealed class EfSharingRepository(PlacesDbContext db) : ISharingRepository
{
    private const string Pending = "PENDING";

    // ── Guest link ───────────────────────────────────────────────────────

    private static GuestLink ToDomain(GuestLinkEntity e) =>
        GuestLink.Restore(e.Id, e.PlaceId, e.Token, e.CreatedBy, e.CreatedAt, e.DeactivatedAt);

    public async Task<GuestLink?> GetActiveGuestLinkAsync(Guid placeId, CancellationToken ct)
    {
        var entity = await db.GuestLinks.AsNoTracking()
            .FirstOrDefaultAsync(l => l.PlaceId == placeId && l.DeactivatedAt == null, ct);   // UX_glink_active_place
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<GuestLink?> FindActiveGuestLinkByTokenAsync(string token, CancellationToken ct)
    {
        var entity = await db.GuestLinks.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Token == token && l.DeactivatedAt == null, ct);       // UQ_glink_token
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddGuestLinkAsync(GuestLink link, GuestLink? replaced, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (replaced?.DeactivatedAt is { } at)
        {
            await db.GuestLinks.Where(l => l.Id == replaced.Id && l.DeactivatedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.DeactivatedAt, at), ct);
        }
        db.GuestLinks.Add(new GuestLinkEntity
        {
            Id = link.Id, PlaceId = link.PlaceId, Token = link.Token, CreatedBy = link.CreatedBy,
            CreatedAt = link.CreatedAt, DeactivatedAt = link.DeactivatedAt,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two requests created the link at the same time: the other one won, and that is the link of the place
            return;
        }
        await tx.CommitAsync(ct);
    }

    public async Task UpdateGuestLinkAsync(GuestLink link, CancellationToken ct)
    {
        var at = link.DeactivatedAt;
        await db.GuestLinks.Where(l => l.Id == link.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.DeactivatedAt, at), ct);
    }

    // ── Invitations ──────────────────────────────────────────────────────

    private static string ToDb(InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => Pending,
        InvitationStatus.Accepted => "ACCEPTED",
        InvitationStatus.Rejected => "REJECTED",
        InvitationStatus.Expired => "EXPIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static InvitationStatus FromDb(string value) => value switch
    {
        Pending => InvitationStatus.Pending,
        "ACCEPTED" => InvitationStatus.Accepted,
        "REJECTED" => InvitationStatus.Rejected,
        "EXPIRED" => InvitationStatus.Expired,
        _ => throw new InvalidOperationException($"Unknown invitation status in the database: '{value}'."),
    };

    private static Invitation ToDomain(InvitationEntity e) => Invitation.Restore(
        e.Id, e.PlaceId, e.InvitedBy, e.Email, PermissionSet.FromStorage(e.Permissions), FromDb(e.Status),
        e.ExpiresAt, e.CreatedAt, e.UpdatedAt, e.RespondedAt);

    private static InvitationEntity ToEntity(Invitation i) => new()
    {
        Id = i.Id, PlaceId = i.PlaceId, InvitedBy = i.InvitedBy, Email = i.Email, Permissions = i.Permissions.ToStorage(),
        Status = ToDb(i.Status), ExpiresAt = i.ExpiresAt, CreatedAt = i.CreatedAt, UpdatedAt = i.UpdatedAt,
        RespondedAt = i.RespondedAt,
    };

    public async Task<Invitation?> GetInvitationAsync(Guid invitationId, CancellationToken ct)
    {
        var entity = await db.Invitations.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invitationId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<Invitation?> FindPendingInvitationAsync(Guid placeId, string email, CancellationToken ct)
    {
        var entity = await db.Invitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.PlaceId == placeId && i.Email == email && i.Status == Pending, ct);   // UX_inv_pending
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Invitation>> ListInvitationsAsync(Guid placeId, CancellationToken ct)
    {
        var rows = await db.Invitations.AsNoTracking().Where(i => i.PlaceId == placeId).ToListAsync(ct);   // IX_inv_place
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingInvitationsForEmailAsync(string email, CancellationToken ct)
    {
        var rows = await db.Invitations.AsNoTracking()
            .Where(i => i.Email == email && i.Status == Pending).ToListAsync(ct);                          // IX_inv_email_status
        return rows.Select(ToDomain).ToList();
    }

    public async Task AddInvitationAsync(Invitation invitation, CancellationToken ct)
    {
        db.Invitations.Add(ToEntity(invitation));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvitationAlreadyPendingException(invitation.Email);   // two clicks at the same time
        }
    }

    public async Task UpdateInvitationAsync(Invitation invitation, CancellationToken ct)
    {
        var status = ToDb(invitation.Status);
        var rows = await db.Invitations.Where(i => i.Id == invitation.Id).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.Status, status)
            .SetProperty(i => i.UpdatedAt, invitation.UpdatedAt)
            .SetProperty(i => i.RespondedAt, invitation.RespondedAt), ct);
        if (rows == 0) throw new InvitationNotFoundException(invitation.Id);
    }

    // ── Collaborators ────────────────────────────────────────────────────

    private static Member ToDomain(PlaceMemberEntity e) => Member.Restore(
        e.Id, e.PlaceId, e.UserId, PermissionSet.FromStorage(e.Permissions), e.CreatedAt, e.UpdatedAt, e.RevokedAt);

    public async Task<Member?> FindMemberAsync(Guid placeId, Guid userId, CancellationToken ct)
    {
        var entity = await db.PlaceMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.PlaceId == placeId && m.UserId == userId, ct);   // UQ_pmem_place_user
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<Member?> GetActiveMemberAsync(Guid placeId, Guid memberId, CancellationToken ct)
    {
        var entity = await db.PlaceMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.PlaceId == placeId && m.RevokedAt == null, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Member>> ListActiveMembersAsync(Guid placeId, CancellationToken ct)
    {
        var rows = await db.PlaceMembers.AsNoTracking()
            .Where(m => m.PlaceId == placeId && m.RevokedAt == null).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Member>> ListActiveMembershipsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.PlaceMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RevokedAt == null).ToListAsync(ct);      // IX_pmem_user
        return rows.Select(ToDomain).ToList();
    }

    public async Task UpdateMemberAsync(Member member, CancellationToken ct)
    {
        var permissions = member.Permissions.ToStorage();
        var rows = await db.PlaceMembers.Where(m => m.Id == member.Id && m.RevokedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Permissions, permissions)
            .SetProperty(m => m.UpdatedAt, member.UpdatedAt), ct);
        if (rows == 0) throw new MemberNotFoundException(member.Id);
    }

    public async Task AcceptInvitationAsync(Invitation accepted, Member member, bool memberIsNew, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Only a still-pending invitation can be accepted: two clicks at the same time, the second one loses
        var status = ToDb(accepted.Status);
        var closed = await db.Invitations.Where(i => i.Id == accepted.Id && i.Status == Pending).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.Status, status)
            .SetProperty(i => i.UpdatedAt, accepted.UpdatedAt)
            .SetProperty(i => i.RespondedAt, accepted.RespondedAt), ct);
        if (closed == 0) throw new InvitationNotPendingException(accepted.Id, InvitationStatus.Accepted);

        var permissions = member.Permissions.ToStorage();
        if (memberIsNew)
        {
            db.PlaceMembers.Add(new PlaceMemberEntity
            {
                Id = member.Id, PlaceId = member.PlaceId, UserId = member.UserId, Role = "COLLABORATOR",
                Permissions = permissions, CreatedAt = member.CreatedAt, UpdatedAt = member.UpdatedAt,
                RevokedAt = member.RevokedAt,
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new AlreadyMemberException(accepted.Email);
            }
        }
        else
        {
            await db.PlaceMembers.Where(m => m.Id == member.Id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Permissions, permissions)
                .SetProperty(m => m.UpdatedAt, member.UpdatedAt)
                .SetProperty(m => m.RevokedAt, (DateTime?)null), ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task RevokeMemberAsync(Member revoked, PlaceActivity activity, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.PlaceMembers.Where(m => m.Id == revoked.Id && m.RevokedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.RevokedAt, revoked.RevokedAt)
            .SetProperty(m => m.UpdatedAt, revoked.UpdatedAt), ct);
        if (rows == 0) throw new MemberNotFoundException(revoked.Id);   // two revokes at the same time: the second one loses

        db.ActivityLog.Add(new PlaceActivityEntity
        {
            PlaceId = activity.PlaceId,
            OwnerId = activity.OwnerId,
            Action = activity.Action,
            Metadata = activity.Metadata,
            CreatedAt = activity.OccurredAt,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
