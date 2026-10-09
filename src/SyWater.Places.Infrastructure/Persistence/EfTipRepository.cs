using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Tips;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;

/// <summary>
/// Outbound adapter for the tips and their favorites. places_rw can UPDATE tips but never DELETE them (v1.3 DCL):
/// a tip is deactivated, not removed. Only tip_favorites rows are deleted, when the user un-marks one.
/// </summary>
public sealed class EfTipRepository(PlacesDbContext db) : ITipRepository, ITipFavoriteRepository
{
    private const string Residential = "RESIDENTIAL";
    private const string Commercial = "COMMERCIAL";

    private static string ToDb(TipCategory category) => category == TipCategory.Commercial ? Commercial : Residential;

    private static TipCategory FromDb(string value) => value == Commercial ? TipCategory.Commercial : TipCategory.Residential;

    private static Tip ToDomain(TipEntity e) => Tip.Restore(
        e.Id, e.Title, e.Body, FromDb(e.Category), e.IsActive, e.CreatedBy, e.CreatedAt, e.UpdatedBy, e.UpdatedAt);

    // ── ITipRepository ───────────────────────────────────────────────────

    public async Task<Tip?> GetAsync(Guid tipId, CancellationToken ct)
    {
        var entity = await db.Tips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tipId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Tip>> ListAsync(TipCategory? category, bool includeInactive, CancellationToken ct)
    {
        var query = db.Tips.AsNoTracking().AsQueryable();
        if (category is { } c)
        {
            var kind = ToDb(c);
            query = query.Where(t => t.Category == kind);
        }
        if (!includeInactive) query = query.Where(t => t.IsActive);

        var rows = await query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Tip>> ListActiveAsync(TipCategory category, CancellationToken ct)
    {
        var kind = ToDb(category);
        var rows = await db.Tips.AsNoTracking()
            .Where(t => t.Category == kind && t.IsActive)                    // IX_tips_category_active
            .ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Tip tip, CancellationToken ct)
    {
        db.Tips.Add(new TipEntity
        {
            Id = tip.Id, Title = tip.Title, Body = tip.Body, Category = ToDb(tip.Category), IsActive = tip.IsActive,
            CreatedBy = tip.CreatedBy, CreatedAt = tip.CreatedAt, UpdatedBy = tip.UpdatedBy, UpdatedAt = tip.UpdatedAt,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Tip tip, CancellationToken ct)
    {
        var category = ToDb(tip.Category);
        var rows = await db.Tips.Where(t => t.Id == tip.Id).ExecuteUpdateAsync(s => s
            .SetProperty(t => t.Title, tip.Title)
            .SetProperty(t => t.Body, tip.Body)
            .SetProperty(t => t.Category, category)
            .SetProperty(t => t.IsActive, tip.IsActive)
            .SetProperty(t => t.UpdatedBy, tip.UpdatedBy)
            .SetProperty(t => t.UpdatedAt, tip.UpdatedAt), ct);

        if (rows == 0) throw new TipNotFoundException(tip.Id);
    }

    // ── ITipFavoriteRepository ───────────────────────────────────────────

    public async Task<IReadOnlySet<Guid>> ListIdsAsync(Guid userId, CancellationToken ct) =>
        (await db.TipFavorites.AsNoTracking().Where(f => f.UserId == userId).Select(f => f.TipId).ToListAsync(ct)).ToHashSet();

    public async Task AddAsync(Guid userId, Guid tipId, CancellationToken ct)
    {
        if (await db.TipFavorites.AnyAsync(f => f.UserId == userId && f.TipId == tipId, ct)) return;
        db.TipFavorites.Add(new TipFavoriteEntity { UserId = userId, TipId = tipId, CreatedAt = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when ((ex.InnerException as SqlException) is { Number: 2601 or 2627 })
        {
            // Two taps at the same time: the other one won and the tip is a favorite, which is what was asked
            db.ChangeTracker.Clear();
        }
    }

    public Task RemoveAsync(Guid userId, Guid tipId, CancellationToken ct) =>
        db.TipFavorites.Where(f => f.UserId == userId && f.TipId == tipId).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Tip>> ListActiveTipsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await (from f in db.TipFavorites.AsNoTracking()
                          join t in db.Tips.AsNoTracking() on f.TipId equals t.Id
                          where f.UserId == userId && t.IsActive
                          orderby f.CreatedAt descending
                          select t).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }
}
