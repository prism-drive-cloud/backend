using System;
using System.Security.Claims;

namespace miniDriveBackend.Api.Extensions
{
    /// <summary>
    /// Helpers para leer los claims que ITokenService coloca en el JWT
    /// (UserId, TenantId, Role) desde cualquier controller autenticado.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var userId))
            {
                throw new InvalidOperationException("El token no contiene un userId válido.");
            }
            return userId;
        }

        public static Guid GetTenantId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue("tenantId");
            if (!Guid.TryParse(value, out var tenantId))
            {
                throw new InvalidOperationException("El token no contiene un tenantId válido.");
            }
            return tenantId;
        }

        public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
            => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        public static bool TryGetTenantId(this ClaimsPrincipal user, out Guid tenantId)
            => Guid.TryParse(user.FindFirstValue("tenantId"), out tenantId);
    }
}