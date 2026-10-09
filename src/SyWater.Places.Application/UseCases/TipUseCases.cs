using System.Security.Cryptography;
using System.Text;
using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tips;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Application.UseCases;

public sealed class CreateTipUseCase(ITipRepository tips, TimeProvider clock) : ICreateTipUseCase
{
    public async Task<AdminTipView> ExecuteAsync(SaveTipCommand command, CancellationToken ct)
    {
        var tip = Tip.Create(command.Title, command.Body, command.Category, command.AdministratorId,
            clock.GetUtcNow().UtcDateTime);
        await tips.AddAsync(tip, ct);
        return AdminTipView.From(tip);
    }
}

public sealed class EditTipUseCase(ITipRepository tips, TimeProvider clock) : IEditTipUseCase
{
    public async Task<AdminTipView> ExecuteAsync(SaveTipCommand command, CancellationToken ct)
    {
        var id = command.TipId ?? throw new InvalidTipException("The tip to edit is required.");
        var tip = await tips.GetAsync(id, ct) ?? throw new TipNotFoundException(id);

        tip.Edit(command.Title, command.Body, command.Category, command.AdministratorId, clock.GetUtcNow().UtcDateTime);
        await tips.UpdateAsync(tip, ct);
        return AdminTipView.From(tip);
    }
}

public sealed class SetTipActiveUseCase(ITipRepository tips, TimeProvider clock) : ISetTipActiveUseCase
{
    public async Task<AdminTipView> ExecuteAsync(Guid administratorId, Guid tipId, bool active, CancellationToken ct)
    {
        var tip = await tips.GetAsync(tipId, ct) ?? throw new TipNotFoundException(tipId);

        var before = tip.IsActive;
        tip.SetActive(active, administratorId, clock.GetUtcNow().UtcDateTime);
        if (tip.IsActive != before) await tips.UpdateAsync(tip, ct);
        return AdminTipView.From(tip);
    }
}

public sealed class ListAdminTipsUseCase(ITipRepository tips) : IListAdminTipsUseCase
{
    public async Task<IReadOnlyList<AdminTipView>> ExecuteAsync(TipCategory? category, bool includeInactive, CancellationToken ct) =>
        (await tips.ListAsync(category, includeInactive, ct)).Select(AdminTipView.From).ToList();
}

/// <summary>HU-065. At least 3 different tips, of the kind of place of the user, in an order that changes every day.</summary>
public sealed class GetTipsUseCase(
    IPlaceRepository places, ITipRepository tips, ITipFavoriteRepository favorites, TimeProvider clock) : IGetTipsUseCase
{
    public const int MinCount = 3;
    public const int DefaultCount = 5;
    public const int MaxCount = 10;

    public async Task<TipsSessionView> ExecuteAsync(
        Guid userId, Guid? placeId, TipCategory? category, int? count, CancellationToken ct)
    {
        var kind = await ResolveCategoryAsync(userId, placeId, category, ct);
        var wanted = Math.Clamp(count ?? DefaultCount, MinCount, MaxCount);

        var available = await tips.ListActiveAsync(kind, ct);
        var marked = await favorites.ListIdsAsync(userId, ct);

        // Every tip appears at most once. The order is stable during the day (a user who reopens the app sees the same
        // tips) and different tomorrow, so over the days everybody gets to read all of them.
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var chosen = available
            .OrderBy(t => Rank(userId, day, t.Id))
            .Take(wanted)
            .Select(t => TipView.From(t, marked.Contains(t.Id)))
            .ToList();

        return new TipsSessionView(kind, available.Count, chosen);
    }

    private async Task<TipCategory> ResolveCategoryAsync(Guid userId, Guid? placeId, TipCategory? category, CancellationToken ct)
    {
        if (placeId is { } id)
        {
            var place = await places.GetActiveAsync(id, userId, ct) ?? throw new PlaceNotFoundException(id);
            return TipCategories.For(place.Type);
        }
        if (category is { } explicitCategory) return explicitCategory;

        var own = await places.ListActiveAsync(userId, ct);
        var selected = own.FirstOrDefault(p => p.IsDefault) ?? own.FirstOrDefault();
        return selected is null ? TipCategory.Residential : TipCategories.For(selected.Type);
    }

    /// <summary>A stable pseudo-random number: the same for the same user, day and tip, in any process.</summary>
    private static int Rank(Guid userId, DateOnly day, Guid tipId) =>
        BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}|{day:yyyyMMdd}|{tipId:N}")));
}

public sealed class ManageTipFavoritesUseCase(ITipRepository tips, ITipFavoriteRepository favorites) : IManageTipFavoritesUseCase
{
    public async Task<TipView> AddAsync(Guid userId, Guid tipId, CancellationToken ct)
    {
        // A deactivated tip is gone for the users: it cannot be marked
        var tip = await tips.GetAsync(tipId, ct);
        if (tip is null || !tip.IsActive) throw new TipNotFoundException(tipId);

        await favorites.AddAsync(userId, tipId, ct);
        return TipView.From(tip, isFavorite: true);
    }

    public Task RemoveAsync(Guid userId, Guid tipId, CancellationToken ct) => favorites.RemoveAsync(userId, tipId, ct);

    public async Task<IReadOnlyList<TipView>> ListAsync(Guid userId, CancellationToken ct) =>
        (await favorites.ListActiveTipsAsync(userId, ct)).Select(t => TipView.From(t, isFavorite: true)).ToList();
}
