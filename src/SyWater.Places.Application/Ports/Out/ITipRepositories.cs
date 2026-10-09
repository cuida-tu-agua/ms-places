using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Application.Ports.Out;

/// <summary>Outbound port: the tips written by the administrators. Tips are never deleted, only deactivated.</summary>
public interface ITipRepository
{
    Task<Tip?> GetAsync(Guid tipId, CancellationToken ct);

    /// <summary>HU-063/064: every tip (optionally only one category), inactive ones included when asked, newest first.</summary>
    Task<IReadOnlyList<Tip>> ListAsync(TipCategory? category, bool includeInactive, CancellationToken ct);

    /// <summary>HU-065: the active tips of a kind of place.</summary>
    Task<IReadOnlyList<Tip>> ListActiveAsync(TipCategory category, CancellationToken ct);

    Task AddAsync(Tip tip, CancellationToken ct);

    Task UpdateAsync(Tip tip, CancellationToken ct);
}

/// <summary>Outbound port: the tips each user marked as favorite (HU-065).</summary>
public interface ITipFavoriteRepository
{
    /// <summary>Ids of the tips the user has marked, to put the star on the right ones.</summary>
    Task<IReadOnlySet<Guid>> ListIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>Idempotent: marking a tip that is already a favorite changes nothing.</summary>
    Task AddAsync(Guid userId, Guid tipId, CancellationToken ct);

    /// <summary>Idempotent: un-marking a tip that is not a favorite changes nothing.</summary>
    Task RemoveAsync(Guid userId, Guid tipId, CancellationToken ct);

    /// <summary>The ACTIVE tips the user marked, newest mark first.</summary>
    Task<IReadOnlyList<Tip>> ListActiveTipsAsync(Guid userId, CancellationToken ct);
}
