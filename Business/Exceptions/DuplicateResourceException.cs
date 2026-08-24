using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class DuplicateResourceException : BusinessException
    {
        public string ResourceType { get; }
        public string Field { get; }
        public string Value { get; }

        public DuplicateResourceException(string resourceType, string field, string value)
            : base($"{resourceType} with {field} '{value}' already exists", "DUPLICATE_RESOURCE", 409)
        {
            ResourceType = resourceType;
            Field = field;
            Value = value;
        }
    }
}