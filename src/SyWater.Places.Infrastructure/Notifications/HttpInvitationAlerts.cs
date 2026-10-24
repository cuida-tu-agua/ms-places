using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;

namespace SyWater.Places.Infrastructure.Notifications;

/// <summary>
/// ms-notification, with the shared X-Internal-Key: "POST /internal/invitations" (the invited person's notification center,
/// push and WhatsApp / SMS) and "POST /internal/invitations/answered" (the owner learns the answer). Best effort: any
/// failure is logged and answered false, the invitation is already saved.
/// </summary>
public sealed class HttpInvitationAlerts(HttpClient http, string internalKey, ILogger<HttpInvitationAlerts> logger) : IInvitationAlerts
{
    public const string InternalKeyHeader = "X-Internal-Key";

    public Task<bool> InvitationSentAsync(InvitationAlert alert, CancellationToken ct) =>
        PostAsync("internal/invitations", new
        {
            invitationId = alert.InvitationId,
            placeId = alert.PlaceId,
            placeName = alert.PlaceName,
            inviteeUserId = alert.InviteeUserId,
            invitedByName = alert.InvitedByName,
            permissions = alert.Permissions,
            expiresAt = alert.ExpiresAt,
        }, ct);

    public Task<bool> InvitationAnsweredAsync(InvitationAnswerAlert alert, CancellationToken ct) =>
        PostAsync("internal/invitations/answered", new
        {
            invitationId = alert.InvitationId,
            placeId = alert.PlaceId,
            placeName = alert.PlaceName,
            ownerId = alert.OwnerId,
            inviteeName = alert.InviteeName,
            accepted = alert.Accepted,
        }, ct);

    private async Task<bool> PostAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(InternalKeyHeader, internalKey);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;
            logger.LogWarning("ms-notification answered {Status} to {Path}", (int)response.StatusCode, path);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "ms-notification could not be reached ({Path})", path);
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "ms-notification did not answer in time ({Path})", path);
            return false;
        }
    }
}
