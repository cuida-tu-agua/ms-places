namespace SyWater.Places.Application.Ports.Out;

public interface IDeviceLinkChecker
{
    /// <exception cref="ExternalServiceUnavailableException">device-service did not answer.</exception>
    Task<bool> HasActiveDeviceAsync(Guid placeId, CancellationToken ct);
}

public sealed class ExternalServiceUnavailableException(string service, Exception? inner = null)
    : Exception($"The service '{service}' is not available right now.", inner)
{
    public string Service { get; } = service;
}