using System;

namespace miniDriveBackend.Business.Exceptions
{
    public abstract class BusinessException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }

        protected BusinessException(string message, string errorCode, int statusCode = 400)
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}