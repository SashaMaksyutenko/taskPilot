using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class GitHubConnectClient : IGitHubConnectClient
{
    private const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserEndpoint = "https://api.github.com/user";
    private const string ReposEndpoint = "https://api.github.com/user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member";

    private readonly HttpClient _http;
    private readonly GitHubIntegrationOptions _options;
    private readonly ILogger<GitHubConnectClient> _logger;

    public GitHubConnectClient(HttpClient http, IOptions<GitHubIntegrationOptions> options, ILogger<GitHubConnectClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        // GitHub rejects API requests without a User-Agent.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TaskPilot");
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["client_id"] = _options.ClientId;
        q["redirect_uri"] = redirectUri;
        q["scope"] = _options.Scope;
        q["state"] = state;
        return $"{AuthorizeEndpoint}?{q}";
    }

    /// <inheritdoc />
    public async Task<Result<GitHubTokenResult>> ExchangeCodeAsync(string code, string redirectUri)
    {
        if (!_options.IsConfigured)
            return Result<GitHubTokenResult>.Fail("GitHub integration is not configured.");

        try
        {
            using var tokenReq = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri,
                }),
            };
            tokenReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var tokenResp = await _http.SendAsync(tokenReq);
            if (!tokenResp.IsSuccessStatusCode)
                return Result<GitHubTokenResult>.Fail("Could not connect to GitHub.");

            using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
            var root = tokenDoc.RootElement;
            var token = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var scope = root.TryGetProperty("scope", out var s) ? s.GetString() : null;
            if (string.IsNullOrEmpty(token))
                return Result<GitHubTokenResult>.Fail("GitHub did not return an access token.");

            var login = await GetLoginAsync(token);
            if (login is null)
                return Result<GitHubTokenResult>.Fail("Could not read the GitHub account.");

            return Result<GitHubTokenResult>.Ok(new GitHubTokenResult(token, login, scope));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging GitHub OAuth code.");
            return Result<GitHubTokenResult>.Fail("Could not connect to GitHub.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<GitHubRepo>>> GetReposAsync(string accessToken)
    {
        try
        {
            using var doc = await GetJsonAsync(ReposEndpoint, accessToken);
            if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
                return Result<List<GitHubRepo>>.Fail("Could not read repositories.");

            var repos = doc.RootElement.EnumerateArray()
                .Select(r => new GitHubRepo(
                    r.TryGetProperty("full_name", out var fn) ? fn.GetString() ?? "" : "",
                    r.TryGetProperty("private", out var pv) && pv.GetBoolean()))
                .Where(r => r.FullName.Length > 0)
                .ToList();
            return Result<List<GitHubRepo>>.Ok(repos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing GitHub repositories.");
            return Result<List<GitHubRepo>>.Fail("Could not read repositories.");
        }
    }

    private async Task<string?> GetLoginAsync(string token)
    {
        using var doc = await GetJsonAsync(UserEndpoint, token);
        return doc?.RootElement.TryGetProperty("login", out var l) == true ? l.GetString() : null;
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    }
}
