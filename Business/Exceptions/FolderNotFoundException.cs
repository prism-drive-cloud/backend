using System;

namespace miniDriveBackend.Business.Exceptions
{
    public class FolderNotFoundException : BusinessException
    {
        public Guid? FolderId { get; }

        public FolderNotFoundException(Guid folderId)
            : base($"Folder with ID '{folderId}' not found", "FOLDER_NOT_FOUND", 404)
        {
            FolderId = folderId;
        }
    }
}