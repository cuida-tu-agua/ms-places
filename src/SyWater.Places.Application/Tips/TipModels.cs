using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Application.Tips;

/// <summary>HU-063 / HU-064. The administrator comes from the token, never from the body.</summary>
public sealed record SaveTipCommand(Guid AdministratorId, Guid? TipId, string Title, string Body, TipCategory Category);

/// <summary>What a user sees (HU-065): only active tips, with their own favorite mark.</summary>
public sealed record TipView(Guid Id, string Title, string Body, TipCategory Category, bool IsFavorite)
{
    public static TipView From(Tip tip, bool isFavorite) => new(tip.Id, tip.Title, tip.Body, tip.Category, isFavorite);
}

/// <summary>What an administrator sees (HU-063 / HU-064): also the inactive ones and who changed them.</summary>
public sealed record AdminTipView(
    Guid Id, string Title, string Body, TipCategory Category, bool IsActive,
    Guid? CreatedBy, DateTime CreatedAt, Guid? UpdatedBy, DateTime UpdatedAt)
{
    public static AdminTipView From(Tip tip) => new(
        tip.Id, tip.Title, tip.Body, tip.Category, tip.IsActive, tip.CreatedBy, tip.CreatedAt, tip.UpdatedBy, tip.UpdatedAt);
}

/// <summary>The tips of one session (HU-065). Total = how many active tips exist for that kind of place.</summary>
public sealed record TipsSessionView(TipCategory Category, int Total, IReadOnlyList<TipView> Tips);
