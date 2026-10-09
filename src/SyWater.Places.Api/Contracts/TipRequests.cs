using System.ComponentModel.DataAnnotations;
using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Api.Contracts;

/// <summary>HU-063 / HU-064: the form of the administrator. Category is RESIDENTIAL or COMMERCIAL.</summary>
public sealed class SaveTipRequest
{
    [Required, StringLength(Tip.TitleMaxLength)] public string? Title { get; init; }
    [Required, StringLength(Tip.BodyMaxLength)] public string? Body { get; init; }
    [Required] public TipCategory? Category { get; init; }
}

public sealed class SetTipActiveRequest
{
    [Required] public bool? Active { get; init; }
}
