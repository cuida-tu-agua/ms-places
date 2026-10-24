using System.Security.Cryptography;
using SyWater.Places.Application.Ports.Out;

namespace SyWater.Places.Infrastructure.Sharing;

/// <summary>32 random bytes from the operating system, in URL-safe base64 (43 characters, 256 bits): not guessable.</summary>
public sealed class RandomGuestTokenGenerator : IGuestTokenGenerator
{
    public string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
