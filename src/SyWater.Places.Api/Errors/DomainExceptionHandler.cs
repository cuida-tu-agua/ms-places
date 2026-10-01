using Microsoft.AspNetCore.Diagnostics;
using SyWater.Places.Domain.Common;
using SyWater.Places.Domain.Places;

namespace SyWater.Places.Api.Errors;


public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code) = exception switch
        {
            PlaceNotFoundException e => (StatusCodes.Status404NotFound, e.Code),
            InvalidPlaceException e => (StatusCodes.Status400BadRequest, e.Code),
            CityNotFoundException e => (StatusCodes.Status400BadRequest, e.Code),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "auth.invalid_token"),
            _ => (0, ""),
        };

        if (status == 0) return false; 

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = code,
                Detail = exception.Message,
                Type = exception is DomainException ? $"https://sywater.dev/errors/{code}" : null,
            },
        });
    }
}