using System.ComponentModel.DataAnnotations;
using SyWater.Places.Application.Sharing;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Sharing;

namespace SyWater.Places.Api.Contracts;

public sealed class InviteCollaboratorRequest
{
    [Required, StringLength(Invitation.EmailMaxLength)] public string? Email { get; init; }

    /// <summary>VIEW_CONSUMPTION is always granted, whether it is listed or not.</summary>
    public string[]? Permissions { get; init; }
}

public sealed class ChangePermissionsRequest
{
    public string[]? Permissions { get; init; }
}

/// <summary>The link for the owner to copy: the token and the address the app opens with it.</summary>
public sealed record GuestLinkResponse(Guid Id, string Token, string Url, DateTime CreatedAt)
{
    public static GuestLinkResponse From(GuestLinkView link, string baseUrl) => new(
        link.Id, link.Token, $"{baseUrl.TrimEnd('/')}/guest/{link.Token}", link.CreatedAt);
}

/// <summary>What a guest gets: no ids, only what is needed to show the water of the place.</summary>
public sealed record GuestPlaceResponse(string Name, string CityName, MeasurementUnit MeasurementUnit)
{
    public static GuestPlaceResponse From(GuestPlaceView view) => new(view.Name, view.CityName, view.MeasurementUnit);
}
