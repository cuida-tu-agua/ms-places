namespace SyWater.Places.Domain.Common;

/// Base class for business-rule violations. The API layer maps each subtype to an HTTP status.
public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}