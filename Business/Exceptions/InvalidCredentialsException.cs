using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class InvalidCredentialsException : BusinessException
    {
        public InvalidCredentialsException(string message = "Invalid email or password")
            : base(message, "INVALID_CREDENTIALS", 401)
        {
        }
    }
}