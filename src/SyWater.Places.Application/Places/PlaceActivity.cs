namespace SyWater.Places.Application.Places;

public sealed record PlaceActivity(
    Guid PlaceId,
    Guid OwnerId,
    string Action,
    string? Metadata,
    DateTime OccurredAt)
{
    public const string PlaceDeleted = "PLACE_DELETED";
}