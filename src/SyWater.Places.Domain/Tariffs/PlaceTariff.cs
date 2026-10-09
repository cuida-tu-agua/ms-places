namespace SyWater.Places.Domain.Tariffs;

/// <summary>Where the prices of a place come from (HU-069 shows it to the user).</summary>
public enum TariffSource
{
    /// <summary>The user typed the price per m³ from their bill (HU-054).</summary>
    Manual,

    /// <summary>The user picked a stratum and the prices come from the tariff catalog of the city (HU-066).</summary>
    Catalog,
}

/// <summary>
/// One entry of the tariff history of a place. Entries are never edited: a change is a NEW entry, and the
/// tariff in force at a date is the newest entry with ValidFrom &lt;= that date, so past costs never change.
/// Prices are per cubic meter (m³), which is how the water bill charges.
/// </summary>
public sealed class PlaceTariff
{
    public const decimal MaxUnitPrice = 1_000_000m;
    public const decimal MaxFixedCharge = 10_000_000m;

    public long Id { get; }
    public Guid PlaceId { get; }
    public TariffSource Source { get; }

    /// <summary>Price per m³. Only for Manual entries; a Catalog entry takes the prices from the catalog.</summary>
    public decimal? UnitPricePerM3 { get; }

    /// <summary>Optional monthly fixed charge of a Manual entry.</summary>
    public decimal? FixedMonthlyCharge { get; }

    /// <summary>Stratum 1-6. Only for Catalog entries.</summary>
    public int? Stratum { get; }

    public DateTime ValidFrom { get; }
    public Guid CreatedBy { get; }

    private PlaceTariff(long id, Guid placeId, TariffSource source, decimal? unitPrice, decimal? fixedCharge,
        int? stratum, DateTime validFrom, Guid createdBy)
    {
        Id = id;
        PlaceId = placeId;
        Source = source;
        UnitPricePerM3 = unitPrice;
        FixedMonthlyCharge = fixedCharge;
        Stratum = stratum;
        ValidFrom = validFrom;
        CreatedBy = createdBy;
    }

    /// <summary>HU-054: the price the user reads on their bill, plus an optional fixed monthly charge.</summary>
    public static PlaceTariff Manual(Guid placeId, decimal unitPricePerM3, decimal? fixedMonthlyCharge, Guid createdBy, DateTime now)
    {
        var price = Math.Round(unitPricePerM3, 2, MidpointRounding.AwayFromZero);
        if (price <= 0 || price > MaxUnitPrice)
            throw new InvalidTariffException($"The price per m³ must be greater than 0 and at most {MaxUnitPrice:0}.");

        decimal? fixedCharge = null;
        if (fixedMonthlyCharge is { } charge)
        {
            fixedCharge = Math.Round(charge, 2, MidpointRounding.AwayFromZero);
            if (fixedCharge < 0 || fixedCharge > MaxFixedCharge)
                throw new InvalidTariffException($"The fixed monthly charge must be between 0 and {MaxFixedCharge:0}.");
        }

        return new PlaceTariff(0, placeId, TariffSource.Manual, price, fixedCharge, null, now, createdBy);
    }

    public const int MinStratum = 1;
    public const int MaxStratum = 6;

    /// <summary>HU-066: the user picks the stratum of their home; the prices come from the catalog of the city.</summary>
    public static PlaceTariff Catalog(Guid placeId, int stratum, Guid createdBy, DateTime now)
    {
        if (stratum is < MinStratum or > MaxStratum)
            throw new InvalidTariffException($"The stratum must be between {MinStratum} and {MaxStratum}.");

        return new PlaceTariff(0, placeId, TariffSource.Catalog, null, null, stratum, now, createdBy);
    }

    /// <summary>Rebuilds an entry read from the database.</summary>
    public static PlaceTariff Restore(long id, Guid placeId, TariffSource source, decimal? unitPrice,
        decimal? fixedCharge, int? stratum, DateTime validFrom, Guid createdBy) =>
        new(id, placeId, source, unitPrice, fixedCharge, stratum, validFrom, createdBy);

    /// <summary>The same stratum again: nothing to add to the history.</summary>
    public bool HasSameStratum(int stratum) => Source == TariffSource.Catalog && Stratum == stratum;

    /// <summary>The same prices again: saving it would only add a duplicate row to the history.</summary>
    public bool HasSameManualPrices(decimal unitPricePerM3, decimal? fixedMonthlyCharge) =>
        Source == TariffSource.Manual
        && UnitPricePerM3 == Math.Round(unitPricePerM3, 2, MidpointRounding.AwayFromZero)
        && (FixedMonthlyCharge ?? 0m) == Math.Round(fixedMonthlyCharge ?? 0m, 2, MidpointRounding.AwayFromZero);
}
