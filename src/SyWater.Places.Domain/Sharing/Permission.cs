namespace SyWater.Places.Domain.Sharing;

/// <summary>What a collaborator may do in a place (HU-044). The owner may do everything (INV-M004).</summary>
public enum Permission
{
    ViewConsumption,
    ViewDeviceStatus,
    ControlValve,
    ViewAlerts,
    ViewBills,
}

/// <summary>The keys of the permissions in the API and in the database.</summary>
public static class PermissionKeys
{
    public static string ToKey(this Permission permission) => permission switch
    {
        Permission.ViewConsumption => "VIEW_CONSUMPTION",
        Permission.ViewDeviceStatus => "VIEW_DEVICE_STATUS",
        Permission.ControlValve => "CONTROL_VALVE",
        Permission.ViewAlerts => "VIEW_ALERTS",
        Permission.ViewBills => "VIEW_BILLS",
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
    };

    public static bool TryParse(string? key, out Permission permission)
    {
        switch (key?.Trim().ToUpperInvariant())
        {
            case "VIEW_CONSUMPTION": permission = Permission.ViewConsumption; return true;
            case "VIEW_DEVICE_STATUS": permission = Permission.ViewDeviceStatus; return true;
            case "CONTROL_VALVE": permission = Permission.ControlValve; return true;
            case "VIEW_ALERTS": permission = Permission.ViewAlerts; return true;
            case "VIEW_BILLS": permission = Permission.ViewBills; return true;
            default: permission = default; return false;
        }
    }
}

/// <summary>
/// The permissions of a collaborator. Immutable. VIEW_CONSUMPTION is always in the set: a collaborator who cannot see
/// the water of the place would have nothing to collaborate on, so asking for fewer than that is not an error, it is
/// just not possible to go below it.
/// </summary>
public sealed class PermissionSet : IEquatable<PermissionSet>
{
    private readonly Permission[] _items;   // enum order, no duplicates

    private PermissionSet(Permission[] items) => _items = items;

    /// <summary>The owner has every permission and cannot lose any.</summary>
    public static PermissionSet Owner { get; } = new(Enum.GetValues<Permission>());

    /// <summary>Builds the set from the keys of the API. An unknown or blank key is an error (HTTP 400).</summary>
    public static PermissionSet FromKeys(IEnumerable<string>? keys)
    {
        var chosen = new HashSet<Permission> { Permission.ViewConsumption };
        foreach (var key in keys ?? [])
        {
            if (!PermissionKeys.TryParse(key, out var permission))
                throw new InvalidPermissionsException($"'{key}' is not a valid permission.");
            chosen.Add(permission);
        }
        return new PermissionSet(chosen.OrderBy(p => (int)p).ToArray());
    }

    /// <summary>Reads what <see cref="ToStorage"/> wrote. A value that does not parse means the data is corrupt.</summary>
    public static PermissionSet FromStorage(string? stored)
    {
        try
        {
            return FromKeys((stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        catch (InvalidPermissionsException ex)
        {
            throw new InvalidOperationException($"Unknown permission stored in the database: '{stored}'.", ex);
        }
    }

    public bool Has(Permission permission) => Array.IndexOf(_items, permission) >= 0;

    /// <summary>The keys in a fixed order, e.g. ["VIEW_CONSUMPTION", "CONTROL_VALVE"].</summary>
    public IReadOnlyList<string> Keys => _items.Select(p => p.ToKey()).ToList();

    /// <summary>"VIEW_CONSUMPTION,CONTROL_VALVE": the column of the database.</summary>
    public string ToStorage() => string.Join(',', Keys);

    public bool Equals(PermissionSet? other) => other is not null && _items.SequenceEqual(other._items);
    public override bool Equals(object? obj) => Equals(obj as PermissionSet);
    public override int GetHashCode() => _items.Aggregate(17, (hash, p) => hash * 31 + (int)p);
    public override string ToString() => ToStorage();
}
