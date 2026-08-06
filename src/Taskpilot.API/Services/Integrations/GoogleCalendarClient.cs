using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real Google Calendar API client — manages events on the user's primary calendar and
/// exchanges/refreshes OAuth tokens. Reuses the same <see cref="GoogleOAuthOptions"/> as
/// Google sign-in (a single OAuth app), so it is enabled whenever those are configured.
/// </summary>
public class GoogleCalendarClient : IGoogleCalendarClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string EventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private readonly HttpClient _http;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleCalendarClient> _logger;

    public GoogleCalendarClient(HttpClient http, IOptions<GoogleOAuthOptions> options, ILogger<GoogleCalendarClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.IsConfigured;

    public Task<Result<GoogleTokenResult>> ExchangeCodeAsync(string code, string redirectUri)
    {
        if (!IsEnabled) return Task.FromResult(Result<GoogleTokenResult>.Fail("Google Calendar sync is not configured."));
        return PostTokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });
    }

    public Task<Result<GoogleTokenResult>> RefreshAccessTokenAsync(string refreshToken)
    {
        if (!IsEnabled) return Task.FromResult(Result<GoogleTokenResult>.Fail("Google Calendar sync is not configured."));
        return PostTokenAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
        });
    }

    private async Task<Result<GoogleTokenResult>> PostTokenAsync(Dictionary<string, string> form)
    {
        try
        {
            var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token request failed. Status: {Status}", resp.StatusCode);
                return Result<GoogleTokenResult>.Fail("Could not authorize with Google Calendar.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            if (string.IsNullOrEmpty(access))
                return Result<GoogleTokenResult>.Fail("Google did not return an access token.");

            var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
            var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            return Result<GoogleTokenResult>.Ok(new GoogleTokenResult(access, refresh, expires));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling the Google token endpoint.");
            return Result<GoogleTokenResult>.Fail("An unexpected error occurred talking to Google.");
        }
    }

    public async Task<Result<string>> UpsertEventAsync(string accessToken, string? eventId, GoogleCalendarEventData ev)
    {
        if (!IsEnabled) return Result<string>.Fail("Google Calendar sync is not configured.");
        try
        {
            var payload = new
            {
                summary = ev.Title,
                description = ev.Description,
                // Google wants RFC3339; the task times are UTC.
                start = new { dateTime = ev.StartUtc.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                end = new { dateTime = ev.EndUtc.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                // Carry the task id so a future pull can match the event back to its task.
                extendedProperties = new { @private = new Dictionary<string, string> { ["taskpilotTaskId"] = ev.TaskId.ToString() } },
            };
            var json = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var req = string.IsNullOrEmpty(eventId)
                ? new HttpRequestMessage(HttpMethod.Post, EventsEndpoint) { Content = json }
                : new HttpRequestMessage(HttpMethod.Put, $"{EventsEndpoint}/{eventId}") { Content = json };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await _http.SendAsync(req);
            // A stored event the user deleted on Google's side: recreate it instead of failing.
            if (!string.IsNullOrEmpty(eventId) && (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.Gone))
                return await UpsertEventAsync(accessToken, null, ev);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google event upsert failed. Status: {Status}", resp.StatusCode);
                return Result<string>.Fail("Could not save the event in Google Calendar.");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            return string.IsNullOrEmpty(id)
                ? Result<string>.Fail("Google Calendar returned no event id.")
                : Result<string>.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error upserting a Google Calendar event.");
            return Result<string>.Fail("An unexpected error occurred talking to Google.");
        }
    }

    public async Task<Result<List<GoogleEventSnapshot>>> ListEventsAsync(string accessToken, DateTime fromUtc, DateTime toUtc)
    {
        if (!IsEnabled) return Result<List<GoogleEventSnapshot>>.Fail("Google Calendar sync is not configured.");
        try
        {
            var q = $"?singleEvents=true&maxResults=2500" +
                    $"&timeMin={Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))}" +
                    $"&timeMax={Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
            using var req = new HttpRequestMessage(HttpMethod.Get, EventsEndpoint + q);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google event list failed. Status: {Status}", resp.StatusCode);
                return Result<List<GoogleEventSnapshot>>.Fail("Could not read events from Google Calendar.");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var list = new List<GoogleEventSnapshot>();
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (string.IsNullOrEmpty(id)) continue;

                    // Only events we created carry taskpilotTaskId in extendedProperties.private.
                    if (!item.TryGetProperty("extendedProperties", out var ext)
                        || !ext.TryGetProperty("private", out var priv)
                        || !priv.TryGetProperty("taskpilotTaskId", out var tid)
                        || !Guid.TryParse(tid.GetString(), out var taskId))
                        continue;

                    // Timed events only (ignore all-day 'date' entries — tasks are timed).
                    if (!item.TryGetProperty("start", out var start)
                        || !start.TryGetProperty("dateTime", out var dtEl)
                        || dtEl.GetString() is not string dt
                        || !DateTimeOffset.TryParse(dt, out var when))
                        continue;

                    list.Add(new GoogleEventSnapshot(id, taskId, when.UtcDateTime));
                }
            }
            return Result<List<GoogleEventSnapshot>>.Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing Google Calendar events.");
            return Result<List<GoogleEventSnapshot>>.Fail("An unexpected error occurred talking to Google.");
        }
    }

    public async Task<Result> DeleteEventAsync(string accessToken, string eventId)
    {
        if (!IsEnabled) return Result.Fail("Google Calendar sync is not configured.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{EventsEndpoint}/{eventId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _http.SendAsync(req);
            // Already gone counts as deleted.
            if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.Gone)
                return Result.Ok();
            _logger.LogWarning("Google event delete failed. Status: {Status}", resp.StatusCode);
            return Result.Fail("Could not delete the event in Google Calendar.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting a Google Calendar event.");
            return Result.Fail("An unexpected error occurred talking to Google.");
        }
    }
}
