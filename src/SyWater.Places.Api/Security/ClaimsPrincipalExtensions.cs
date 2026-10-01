using System.Security.Claims;

namespace SyWater.Places.Api.Security;

public static class ClaimsPrincipalExtensions
{
   
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("The token has no valid 'sub' claim.");
    }
}