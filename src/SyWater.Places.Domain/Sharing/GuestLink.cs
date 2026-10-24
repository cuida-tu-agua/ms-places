namespace SyWater.Places.Domain.Sharing;

/// <summary>
/// The shareable link of a place (HU-039). Whoever has the token sees the consumption of the place and nothing else
/// (HU-040/041). There is no session: every request of the guest carries the token, so deactivating the link cuts
/// the access of everybody at once, which is what "an active anonymous session loses access" asks for.
/// </summary>
public sealed class GuestLink
{
    public const int MinTokenLength = 32;

    public Guid Id { get; }
    public Guid PlaceId { get; }
    public string Token { get; }
    public Guid CreatedBy { get; }
    public DateTime CreatedAt { get; }
    public DateTime? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    private GuestLink(Guid id, Guid placeId, string token, Guid createdBy, DateTime createdAt, DateTime? deactivatedAt)
    {
        Id = id;
        PlaceId = placeId;
        Token = token;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        DeactivatedAt = deactivatedAt;
    }

    public static GuestLink Create(Guid placeId, Guid createdBy, string token, DateTime now)
    {
        if (placeId == Guid.Empty) throw new ArgumentException("Place is required.", nameof(placeId));
        if (createdBy == Guid.Empty) throw new ArgumentException("The owner is required.", nameof(createdBy));
        if (string.IsNullOrWhiteSpace(token) || token.Length < MinTokenLength)
            throw new ArgumentException($"The token needs at least {MinTokenLength} characters.", nameof(token));
        return new GuestLink(Guid.NewGuid(), placeId, token, createdBy, now, null);
    }

    public static GuestLink Restore(Guid id, Guid placeId, string token, Guid createdBy, DateTime createdAt, DateTime? deactivatedAt) =>
        new(id, placeId, token, createdBy, createdAt, deactivatedAt);

    public void Deactivate(DateTime now)
    {
        if (IsActive) DeactivatedAt = now;   // deactivating twice keeps the first date
    }
}
