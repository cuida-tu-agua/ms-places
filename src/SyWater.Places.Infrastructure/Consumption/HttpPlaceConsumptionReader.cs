using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Domain.Tariffs;
using SyWater.Places.Infrastructure.Devices;

namespace SyWater.Places.Infrastructure.Consumption;

/// <summary>
/// Asks ms-consumption for the water of a place with the SAME token the user sent to us, so ms-consumption applies its
/// own access check. Any failure to answer is "service unavailable" (HTTP 503), never a made-up zero.
/// </summary>
public sealed class HttpPlaceConsumptionReader(HttpClient http, IAccessTokenProvider tokens) : IPlaceConsumptionReader
{
    public const string ServiceName = "consumption-service";

    private sealed record ConsumptionDto(DateTime From, DateTime To, decimal TotalLiters, bool HasData);

    public async Task<PeriodConsumption> GetAsync(Guid placeId, string period, string? timeZone, CancellationToken ct)
    {
        var token = tokens.GetAccessToken()
                    ?? throw new InvalidOperationException("There is no access token in the current request.");

        var query = $"period={Uri.EscapeDataString(period)}"
                    + (string.IsNullOrWhiteSpace(timeZone) ? "" : $"&tz={Uri.EscapeDataString(timeZone)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/places/{placeId}/consumption?{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new InvalidTariffException("The time zone is not valid.");   // the period was checked before asking
            if (!response.IsSuccessStatusCode)
                throw new ExternalServiceUnavailableException(ServiceName,
                    new HttpRequestException($"Unexpected status {(int)response.StatusCode}."));

            var dto = await response.Content.ReadFromJsonAsync<ConsumptionDto>(ct)
                      ?? throw new ExternalServiceUnavailableException(ServiceName);
            return new PeriodConsumption(dto.From, dto.To, dto.TotalLiters, dto.HasData);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
    }
}
