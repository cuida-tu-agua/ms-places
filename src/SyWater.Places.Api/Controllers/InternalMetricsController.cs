using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyWater.Places.Api.Security;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.In;

namespace SyWater.Places.Api.Controllers;

/// <summary>Service-to-service only (X-Internal-Key): ms-iam builds the administrator's metrics from it.</summary>
[ApiController]
[AllowAnonymous]
[InternalKey]
[Route("internal/metrics")]
public sealed class InternalMetricsController : ControllerBase
{
    /// <summary>HU-062</summary>
    [HttpGet]
    public async Task<ActionResult<PlaceMetrics>> Get([FromServices] IGetPlaceMetricsUseCase useCase, CancellationToken ct) =>
        Ok(await useCase.ExecuteAsync(ct));
}
