using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real LinkedIn OAuth client (OpenID Connect): swaps the authorization code for
/// tokens at LinkedIn's token endpoint, then reads the account profile from the
/// OIDC userinfo endpoint.
/// </summary>
public class LinkedInAuthClient : ILinkedInAuthClient
{
    private const string TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
    private const string UserInfoEndpoint = "https://api.linkedin.com/v2/userinfo";

    private readonly HttpClient _http;
    private readonly LinkedInOAuthOptions _options;
    private readonly ILogger<LinkedInAuthClient> _logger;

    public LinkedInAuthClient(HttpClient http, IOptions<LinkedInOAuthOptions> options, ILogger<LinkedInAuthClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<LinkedInUserInfo>> ExchangeCodeAsync(string code)
    {
        if (!_options.IsConfigured)
            return Result<LinkedInUserInfo>.Fail("LinkedIn sign-in is not configured.");

        try
        {
            // 1) Exchange the one-time code for an access token.
            var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
            });

            var tokenResponse = await _http.PostAsync(TokenEndpoint, tokenForm);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("LinkedIn token exchange failed. Status: {Status}", tokenResponse.StatusCode);
                return Result<LinkedInUserInfo>.Fail("Could not verify the LinkedIn sign-in.");
            }

            using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenEl))
                return Result<LinkedInUserInfo>.Fail("Could not verify the LinkedIn sign-in.");
            var accessToken = accessTokenEl.GetString();

            // 2) Read the account profile from the OIDC userinfo endpoint.
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var userResponse = await _http.SendAsync(userRequest);
            if (!userResponse.IsSuccessStatusCode)
                return Result<LinkedInUserInfo>.Fail("Could not read the LinkedIn profile.");

            using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var root = userDoc.RootElement;
            var sub = root.TryGetProperty("sub", out var s) ? s.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
                return Result<LinkedInUserInfo>.Fail("The LinkedIn account has no email.");

            return Result<LinkedInUserInfo>.Ok(new LinkedInUserInfo(sub, email, name ?? email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LinkedIn OAuth exchange.");
            return Result<LinkedInUserInfo>.Fail("An unexpected error occurred during LinkedIn sign-in.");
        }
    }
}
