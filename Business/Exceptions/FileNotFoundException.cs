using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class FileNotFoundException : BusinessException
    {
        public Guid? FileId { get; }

        public FileNotFoundException(Guid fileId)
            : base($"File with ID '{fileId}' not found", "FILE_NOT_FOUND", 404)
        {
            FileId = fileId;
        }
    }
}