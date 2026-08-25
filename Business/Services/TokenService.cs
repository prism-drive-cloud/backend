using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using miniDriveBackend.Business.Configuration;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Models;
using AuthValidationResult = miniDriveBackend.Business.Interfaces.TokenValidationResult;

namespace miniDriveBackend.Business.Services
{
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly SymmetricSecurityKey _signingKey;

        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        }

        public string GenerateAccessToken(User user)
        {
            var now = DateTime.UtcNow;

            var claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["role"] = user.Role.ToString(),
                ["jti"] = Guid.NewGuid().ToString(),
                ["email"] = user.Email,
                ["name"] = user.FullName
            };

            // SuperAdmin has no tenant: omit tenant_id entirely rather than inventing one.
            if (user.TenantId.HasValue)
            {
                claims["tenant_id"] = user.TenantId.Value.ToString();
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(_options.AccessTokenExpiryMinutes),
                Claims = claims,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncoder.Encode(bytes);
        }

        public async Task<AuthValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var result = await new JsonWebTokenHandler()
                .ValidateTokenAsync(token, GetValidationParameters(validateLifetime: true));

            if (!result.IsValid)
            {
                return new AuthValidationResult(false, null, null, null, result.Exception?.Message ?? "Invalid token");
            }

            var userId = TryGetGuidClaim(result.Claims, "sub");
            var tenantId = TryGetGuidClaim(result.Claims, "tenant_id");
            UserRole? role = result.Claims.TryGetValue("role", out var roleValue)
                && Enum.TryParse<UserRole>(roleValue?.ToString(), out var parsedRole)
                    ? parsedRole
                    : null;

            return new AuthValidationResult(true, userId, tenantId, role, null);
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var result = new JsonWebTokenHandler()
                .ValidateTokenAsync(token, GetValidationParameters(validateLifetime: false))
                .GetAwaiter()
                .GetResult();

            if (!result.IsValid || result.ClaimsIdentity is null)
            {
                throw new SecurityTokenException("Invalid token");
            }

            return new ClaimsPrincipal(result.ClaimsIdentity);
        }

        private TokenValidationParameters GetValidationParameters(bool validateLifetime)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateLifetime = validateLifetime,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        }

        private static Guid? TryGetGuidClaim(IDictionary<string, object> claims, string key)
        {
            return claims.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id)
                ? id
                : null;
        }
    }
}
