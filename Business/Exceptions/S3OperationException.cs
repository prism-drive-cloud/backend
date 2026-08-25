using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class S3OperationException : BusinessException
    {
        public string Operation { get; }
        public string? S3Key { get; }

        public S3OperationException(string operation, string message, string? s3Key = null)
            : base($"S3 {operation} failed: {message}", "S3_OPERATION_FAILED", 500)
        {
            Operation = operation;
            S3Key = s3Key;
        }
    }
}