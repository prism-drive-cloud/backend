using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Security
{
    public class TenantContext : ITenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

        public Guid? UserId => GetGuidClaim("sub");

        public Guid? TenantId => GetGuidClaim("tenant_id");

        public UserRole? Role
        {
            get
            {
                var value = Principal?.FindFirst("role")?.Value;
                return Enum.TryParse<UserRole>(value, out var role) ? role : null;
            }
        }

        private Guid? GetGuidClaim(string claimType)
        {
            var value = Principal?.FindFirst(claimType)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
