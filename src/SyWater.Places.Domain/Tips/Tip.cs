using SyWater.Places.Domain.Common;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Domain.Tips;

/// <summary>The kind of place a tip is written for (HU-063 category, HU-065 "different for home and business").</summary>
public enum TipCategory
{
    Residential,
    Commercial,
}

public static class TipCategories
{
    /// <summary>The tips a place receives are the ones of its own kind.</summary>
    public static TipCategory For(PlaceType type) =>
        type == PlaceType.Commercial ? TipCategory.Commercial : TipCategory.Residential;
}

/// <summary>Invalid tip data (empty title or body, too long). HTTP 400.</summary>
public sealed class InvalidTipException(string message)
    : DomainException("tip.invalid", message);

/// <summary>The tip does not exist. HTTP 404.</summary>
public sealed class TipNotFoundException(Guid tipId)
    : DomainException("tip.not_found", $"Tip {tipId} was not found.");

/// <summary>
/// A water saving recommendation written by an administrator (HU-063) and shown to every user of that kind of place
/// (HU-065). A tip is never deleted: deactivating it (HU-064) hides it from the users and keeps its history.
/// </summary>
public sealed class Tip
{
    public const int TitleMaxLength = 150;
    public const int BodyMaxLength = 1000;

    public Guid Id { get; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public TipCategory Category { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CreatedBy { get; }
    public DateTime CreatedAt { get; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Tip(Guid id, string title, string body, TipCategory category, bool isActive, Guid? createdBy,
        DateTime createdAt, Guid? updatedBy, DateTime updatedAt)
    {
        Id = id;
        Title = title;
        Body = body;
        Category = category;
        IsActive = isActive;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    /// <summary>HU-063: a new tip is published (visible to everybody) as soon as it is created.</summary>
    public static Tip Create(string title, string body, TipCategory category, Guid administratorId, DateTime now) =>
        new(Guid.NewGuid(), Clean(title, TitleMaxLength, "Title"), Clean(body, BodyMaxLength, "Body"), category,
            isActive: true, administratorId, now, administratorId, now);

    public static Tip Restore(Guid id, string title, string body, TipCategory category, bool isActive,
        Guid? createdBy, DateTime createdAt, Guid? updatedBy, DateTime updatedAt) =>
        new(id, title, body, category, isActive, createdBy, createdAt, updatedBy, updatedAt);

    /// <summary>HU-064: the change applies right away (the next read already sees it).</summary>
    public void Edit(string title, string body, TipCategory category, Guid administratorId, DateTime now)
    {
        Title = Clean(title, TitleMaxLength, "Title");
        Body = Clean(body, BodyMaxLength, "Body");
        Category = category;
        UpdatedBy = administratorId;
        UpdatedAt = now;
    }

    /// <summary>HU-064: false hides it from the users without deleting anything; true brings it back.</summary>
    public void SetActive(bool active, Guid administratorId, DateTime now)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedBy = administratorId;
        UpdatedAt = now;
    }

    private static string Clean(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new InvalidTipException($"{field} is required.");
        if (text.Length > max) throw new InvalidTipException($"{field} must have at most {max} characters.");
        return text;
    }
}
