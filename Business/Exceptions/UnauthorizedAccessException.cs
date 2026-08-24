using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class UnauthorizedAccessException : BusinessException
    {
        public Guid? ResourceId { get; }
        public string? ResourceType { get; }

        public UnauthorizedAccessException(string message = "Unauthorized access to resource")
            : base(message, "UNAUTHORIZED_ACCESS", 403)
        {
        }

        public UnauthorizedAccessException(string resourceType, Guid resourceId)
            : base($"Unauthorized access to {resourceType} with ID '{resourceId}'", "UNAUTHORIZED_ACCESS", 403)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }
}