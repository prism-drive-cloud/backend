using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class UserNotFoundException : BusinessException
    {
        public Guid? UserId { get; }
        public string? Email { get; }

        public UserNotFoundException(Guid userId)
            : base($"User with ID '{userId}' not found", "USER_NOT_FOUND", 404)
        {
            UserId = userId;
        }

        public UserNotFoundException(string email)
            : base($"User with email '{email}' not found", "USER_NOT_FOUND", 404)
        {
            Email = email;
        }
    }
}