using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Sharing;

namespace SyWater.Places.Infrastructure.Iam;

/// <summary>
/// ms-iam, with the shared X-Internal-Key:
/// "GET /internal/users/{id}/contact" (the one ms-notifications already uses) and "GET /internal/users/by-email".
/// Both answer {"id","email","fullName",...}; 404 = no usable account (not found, deleted or e-mail not verified).
/// Any other failure is "service unavailable" (HTTP 503), never an invented answer.
/// </summary>
public sealed class HttpUserDirectory(HttpClient http, string internalKey) : IUserDirectory
{
    public const string ServiceName = "ms-iam";
    public const string InternalKeyHeader = "X-Internal-Key";

    public async Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var found = await Task.WhenAll(ids.Select(id => GetAsync($"internal/users/{id}/contact", ct)));
        return found.Where(c => c is not null).ToDictionary(c => c!.UserId, c => c!);
    }

    public Task<UserContact?> FindByEmailAsync(string email, CancellationToken ct) =>
        GetAsync($"internal/users/by-email?email={Uri.EscapeDataString(email)}", ct);

    private async Task<UserContact?> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(InternalKeyHeader, internalKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
            if (!response.IsSuccessStatusCode) throw new ExternalServiceUnavailableException(ServiceName);

            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var id = json.TryGetProperty("id", out var i) && i.TryGetGuid(out var guid) ? guid : Guid.Empty;
                var email = json.TryGetProperty("email", out var e) ? e.GetString() : null;
                var name = json.TryGetProperty("fullName", out var n) ? n.GetString() : null;
                return id == Guid.Empty || string.IsNullOrWhiteSpace(email) ? null : new UserContact(id, email, name ?? email);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new ExternalServiceUnavailableException(ServiceName, ex);
            }
        }
    }
}

/// <summary>ms-iam "POST /internal/invitation-emails": ms-iam owns the mail server, so it writes and sends the e-mail.</summary>
public sealed class HttpInvitationNotifier(HttpClient http, string internalKey) : IInvitationNotifier
{
    public async Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/invitation-emails")
        {
            Content = JsonContent.Create(new
            {
                invitationId = mail.InvitationId,
                to = mail.ToEmail,
                placeName = mail.PlaceName,
                invitedByName = mail.InvitedByName,
                permissions = mail.Permissions,
                expiresAt = mail.ExpiresAt,
            }),
        };
        request.Headers.Add(HttpUserDirectory.InternalKeyHeader, internalKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;   // the invitation is saved: the owner sees it and can resend
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}

/// <summary>Used while "Services:IamBaseUrl" is empty (local runs without ms-iam): sharing needs it, so it says so (HTTP 503).</summary>
public sealed class IamNotConfiguredUserDirectory : IUserDirectory
{
    public Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        throw new ExternalServiceUnavailableException(HttpUserDirectory.ServiceName);

    public Task<UserContact?> FindByEmailAsync(string email, CancellationToken ct) =>
        throw new ExternalServiceUnavailableException(HttpUserDirectory.ServiceName);
}

public sealed class IamNotConfiguredInvitationNotifier : IInvitationNotifier
{
    public Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken ct) => Task.FromResult(false);
}
