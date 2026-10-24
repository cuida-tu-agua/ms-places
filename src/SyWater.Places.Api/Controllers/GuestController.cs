using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Contracts;
using SyWater.Places.Application.Ports.In;

namespace SyWater.Places.Api.Controllers;

/// <summary>
/// HU-040 · What a guest sees. No login: the token of the link is the credential. Read-only by construction: there is
/// no write endpoint under this route. Unknown, deactivated and deleted all answer 404.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/guest")]
public sealed class GuestController : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<GuestPlaceResponse>> Get(
        string token, [FromServices] IResolveGuestLinkUseCase useCase, CancellationToken ct) =>
        Ok(GuestPlaceResponse.From(await useCase.ExecuteAsync(token, ct)));
}
