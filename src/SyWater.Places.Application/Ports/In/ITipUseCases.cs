using SyWater.Places.Application.Tips;
using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Application.Ports.In;

/// <summary>HU-063: an administrator publishes a tip (title, body and category). Visible to everybody at once.</summary>
public interface ICreateTipUseCase
{
    Task<AdminTipView> ExecuteAsync(SaveTipCommand command, CancellationToken ct);
}

/// <summary>HU-064: an administrator edits a tip. The change applies right away.</summary>
public interface IEditTipUseCase
{
    Task<AdminTipView> ExecuteAsync(SaveTipCommand command, CancellationToken ct);
}

/// <summary>HU-064: deactivate (hidden from the users, not deleted) or reactivate a tip.</summary>
public interface ISetTipActiveUseCase
{
    Task<AdminTipView> ExecuteAsync(Guid administratorId, Guid tipId, bool active, CancellationToken ct);
}

/// <summary>HU-063/064: the tips for the administration screen.</summary>
public interface IListAdminTipsUseCase
{
    Task<IReadOnlyList<AdminTipView>> ExecuteAsync(TipCategory? category, bool includeInactive, CancellationToken ct);
}

/// <summary>
/// HU-065: the tips a user sees. They depend on the kind of place: the place given, else the explicit category, else
/// the kind of the user's selected place. At least 3 different tips are returned whenever that many exist.
/// </summary>
public interface IGetTipsUseCase
{
    Task<TipsSessionView> ExecuteAsync(Guid userId, Guid? placeId, TipCategory? category, int? count, CancellationToken ct);
}

/// <summary>HU-065: mark / un-mark a tip as favorite, and list the favorites.</summary>
public interface IManageTipFavoritesUseCase
{
    Task<TipView> AddAsync(Guid userId, Guid tipId, CancellationToken ct);
    Task RemoveAsync(Guid userId, Guid tipId, CancellationToken ct);
    Task<IReadOnlyList<TipView>> ListAsync(Guid userId, CancellationToken ct);
}
