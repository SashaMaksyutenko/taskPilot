using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real Google OAuth client: swaps the authorization code for tokens at Google's
/// token endpoint, then reads the account profile from the userinfo endpoint.
/// </summary>
public class GoogleAuthClient : IGoogleAuthClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

    private readonly HttpClient _http;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleAuthClient> _logger;

    public GoogleAuthClient(HttpClient http, IOptions<GoogleOAuthOptions> options, ILogger<GoogleAuthClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<GoogleUserInfo>> ExchangeCodeAsync(string code)
    {
        if (!_options.IsConfigured)
            return Result<GoogleUserInfo>.Fail("Google sign-in is not configured.");

        try
        {
            // 1) Exchange the one-time code for an access token.
            var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code",
            });

            var tokenResponse = await _http.PostAsync(TokenEndpoint, tokenForm);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token exchange failed. Status: {Status}", tokenResponse.StatusCode);
                return Result<GoogleUserInfo>.Fail("Could not verify the Google sign-in.");
            }

            using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenEl))
                return Result<GoogleUserInfo>.Fail("Could not verify the Google sign-in.");
            var accessToken = accessTokenEl.GetString();

            // 2) Read the account profile with the access token.
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var userResponse = await _http.SendAsync(userRequest);
            if (!userResponse.IsSuccessStatusCode)
                return Result<GoogleUserInfo>.Fail("Could not read the Google profile.");

            using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var root = userDoc.RootElement;
            var sub = root.TryGetProperty("sub", out var s) ? s.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
                return Result<GoogleUserInfo>.Fail("The Google account has no email.");

            return Result<GoogleUserInfo>.Ok(new GoogleUserInfo(sub, email, name ?? email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Google OAuth exchange.");
            return Result<GoogleUserInfo>.Fail("An unexpected error occurred during Google sign-in.");
        }
    }
}
