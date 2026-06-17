using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Taskpilot.API.Configuration;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Builds and signs JWT access tokens using the configured <see cref="JwtSettings"/>.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;

    /// <summary>
    /// Creates the service. JWT settings are injected via the options pattern.
    /// </summary>
    /// <param name="jwtOptions">Strongly-typed JWT settings from configuration.</param>
    public TokenService(IOptions<JwtSettings> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    /// <summary>
    /// Creates a signed JWT containing the user's id, email and role.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The signed token string and its UTC expiry time.</returns>
    public (string token, DateTime expiresAtUtc) GenerateAccessToken(User user)
    {
        // Claims = pieces of information embedded in the token about the user.
        var claims = new List<Claim>
        {
            // "sub" = subject: the unique user id (standard JWT claim).
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            // "jti" = unique token id, useful for revocation/refresh tracking later.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Role claim drives role-based authorization ([Authorize(Roles = ...)]).
            new(ClaimTypes.Role, user.Role.ToString())
        };

        // Build the signing key from the secret and sign with HMAC-SHA256.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Access tokens are short-lived (15 minutes by default).
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        // Assemble the token with issuer/audience so it can be validated later.
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        // Serialize the token to its compact string form.
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiresAtUtc);
    }
}
