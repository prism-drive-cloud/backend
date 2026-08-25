using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class QuotaExceededException : BusinessException
    {
        public long RequestedBytes { get; }
        public long AvailableBytes { get; }
        public long QuotaBytes { get; }

        public QuotaExceededException(long requestedBytes, long availableBytes, long quotaBytes)
            : base(
                $"Storage quota exceeded. Requested: {requestedBytes} bytes, Available: {availableBytes} bytes, Quota: {quotaBytes} bytes",
                "QUOTA_EXCEEDED",
                400)
        {
            RequestedBytes = requestedBytes;
            AvailableBytes = availableBytes;
            QuotaBytes = quotaBytes;
        }
    }
}