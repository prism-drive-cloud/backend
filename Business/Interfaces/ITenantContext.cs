using System;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface ITenantContext
    {
        Guid? UserId { get; }
        Guid? TenantId { get; }
        UserRole? Role { get; }
        bool IsAuthenticated { get; }
    }
}
