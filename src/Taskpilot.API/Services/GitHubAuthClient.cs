using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real GitHub OAuth client: swaps the authorization code for an access token,
/// then reads the account profile (and primary email) from the GitHub API.
/// </summary>
public class GitHubAuthClient : IGitHubAuthClient
{
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserEndpoint = "https://api.github.com/user";
    private const string EmailsEndpoint = "https://api.github.com/user/emails";

    private readonly HttpClient _http;
    private readonly GitHubOAuthOptions _options;
    private readonly ILogger<GitHubAuthClient> _logger;

    public GitHubAuthClient(HttpClient http, IOptions<GitHubOAuthOptions> options, ILogger<GitHubAuthClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        // GitHub's API requires a User-Agent header on every request.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Taskpilot");
    }

    /// <inheritdoc />
    public async Task<Result<GitHubUserInfo>> ExchangeCodeAsync(string code)
    {
        if (!_options.IsConfigured)
            return Result<GitHubUserInfo>.Fail("GitHub sign-in is not configured.");

        try
        {
            // 1) Exchange the one-time code for an access token (ask for JSON back).
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = _options.RedirectUri,
                }),
            };
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var tokenResponse = await _http.SendAsync(tokenRequest);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub token exchange failed. Status: {Status}", tokenResponse.StatusCode);
                return Result<GitHubUserInfo>.Fail("Could not verify the GitHub sign-in.");
            }

            using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var tokenEl))
                return Result<GitHubUserInfo>.Fail("Could not verify the GitHub sign-in.");
            var accessToken = tokenEl.GetString();

            // 2) Read the account profile.
            var profile = await GetJsonAsync(UserEndpoint, accessToken);
            if (profile is null)
                return Result<GitHubUserInfo>.Fail("Could not read the GitHub profile.");

            var id = profile.Value.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : null;
            var login = profile.Value.TryGetProperty("login", out var loginEl) ? loginEl.GetString() : null;
            var name = profile.Value.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var email = profile.Value.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;

            // 3) GitHub often hides the email on the profile — fetch the primary one.
            if (string.IsNullOrEmpty(email))
                email = await GetPrimaryEmailAsync(accessToken);

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(email))
                return Result<GitHubUserInfo>.Fail("The GitHub account has no verified email.");

            return Result<GitHubUserInfo>.Ok(new GitHubUserInfo(id, email, name ?? login ?? email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GitHub OAuth exchange.");
            return Result<GitHubUserInfo>.Fail("An unexpected error occurred during GitHub sign-in.");
        }
    }

    /// <summary>GETs a GitHub API endpoint with the bearer token and returns the parsed root element.</summary>
    private async Task<JsonElement?> GetJsonAsync(string url, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>Returns the user's primary, verified email, or null.</summary>
    private async Task<string?> GetPrimaryEmailAsync(string? accessToken)
    {
        var emails = await GetJsonAsync(EmailsEndpoint, accessToken);
        if (emails is not { ValueKind: JsonValueKind.Array })
            return null;

        foreach (var entry in emails.Value.EnumerateArray())
        {
            var primary = entry.TryGetProperty("primary", out var p) && p.GetBoolean();
            var verified = entry.TryGetProperty("verified", out var v) && v.GetBoolean();
            if (primary && verified && entry.TryGetProperty("email", out var e))
                return e.GetString();
        }
        return null;
    }
}
