using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class TenantNotFoundException : BusinessException
    {
        public Guid? TenantId { get; }
        public string? Slug { get; }

        public TenantNotFoundException(Guid tenantId)
            : base($"Tenant with ID '{tenantId}' not found", "TENANT_NOT_FOUND", 404)
        {
            TenantId = tenantId;
        }

        public TenantNotFoundException(string slug)
            : base($"Tenant with slug '{slug}' not found", "TENANT_NOT_FOUND", 404)
        {
            Slug = slug;
        }
    }
}