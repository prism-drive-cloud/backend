using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Business.DTOs
{
    public record CreateFolderRequest(
        [Required][MaxLength(255)] string Name,
        Guid? ParentFolderId = null
    );

    public record FolderResponse(
        Guid Id,
        Guid TenantId,
        Guid OwnerId,
        string Name,
        Guid? ParentFolderId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public record RenameFolderRequest(
        [Required][MaxLength(255)] string NewName
    );

    public record FolderTreeResponse(
        FolderResponse Folder,
        IReadOnlyList<FolderTreeResponse> Children
    );
}