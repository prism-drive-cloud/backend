# DTOs (Data Transfer Objects) - Purpose and Design

## What is a DTO?

A **DTO** is a plain data container used to transfer data between layers or across process boundaries. It has no behavior—only properties.

```csharp
public record FileResponse(
    Guid Id,
    Guid TenantId,
    Guid OwnerId,
    Guid? FolderId,
    string OriginalName,
    string MimeType,
    long SizeBytes,
    string S3Key,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
```

## Why Use DTOs Instead of Entities?

### 1. **Separation of Concerns**
- **Entities** (Domain Layer): Rich objects with behavior, validation, navigation properties, EF Core mappings
- **DTOs** (Business/Presentation): Flat, serializable, API-shaped data

### 2. **Security (Prevent Over-Posting)**
- Entities may have sensitive fields (PasswordHash, InternalId, AuditFields)
- DTOs expose only what the API consumer needs

### 3. **API Versioning & Stability**
- Entity changes don't break API contracts
- Can add computed fields (UsagePercentage, DownloadUrl) without DB changes
- Different DTOs for different endpoints (Create vs List vs Detail)

### 4. **Serialization Control**
- Records provide immutable, clean JSON output
- No circular reference issues (common with EF Core navigation properties)
- Explicit control over property names (camelCase vs PascalCase)

### 5. **Performance**
- DTOs can be tailored to exact needs (e.g., `FileListItemDto` vs `FileDetailDto`)
- Avoids over-fetching data from database

## DTO Categories in This Project

### Request DTOs (Input)
Used for incoming API requests. Include validation attributes.

```csharp
public record UploadUrlRequest(
    [Required][MaxLength(255)] string FileName,
    [Required][MaxLength(100)] string MimeType,
    [Required][Range(1, long.MaxValue)] long SizeBytes,
    Guid? FolderId = null
);
```

### Response DTOs (Output)
Used for API responses. Often include computed/aggregated data.

```csharp
public record TenantUsageResponse(
    Guid TenantId,
    string TenantName,
    long UsedBytes,
    long QuotaBytes,
    long AvailableBytes,
    double UsagePercentage,  // Computed
    int FileCount,           // Aggregated
    int FolderCount          // Aggregated
);
```

### Query/Parameter DTOs
Encapsulate filtering, pagination, sorting parameters.

```csharp
public record FileQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    Guid? FolderId = null,
    string? MimeType = null,
    string SortBy = "CreatedAt",
    string SortOrder = "desc"
);
```

### Wrapper DTOs
Generic containers for consistent API responses.

```csharp
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
```

## DTO Catalog

| File | DTOs | Purpose |
|------|------|---------|
| `AuthDtos.cs` | LoginRequest, RegisterTenantRequest, RegisterUserRequest, AuthResponse, UserProfileResponse, TokenRefreshRequest, ChangePasswordRequest | Authentication flows |
| `TenantDtos.cs` | CreateTenantRequest, TenantResponse, TenantUsageResponse | Tenant management & analytics |
| `FileDtos.cs` | FileQueryParameters, FileResponse, UploadUrlRequest/Response, ConfirmUploadRequest, DownloadUrlResponse, RenameFileRequest, MoveFileRequest, PagedResult | File operations + S3 flow |
| `FolderDtos.cs` | CreateFolderRequest, FolderResponse, RenameFolderRequest, FolderTreeResponse | Folder hierarchy |
| `UserDtos.cs` | CreateUserRequest, UserResponse, UpdateUserRequest | User management |

## Why `record` Types?

```csharp
// Immutable, value-based equality, with-expression support
public record FileResponse(
    Guid Id,
    string OriginalName,
    long SizeBytes
);

// Usage
var updated = file with { SizeBytes = 2048 };
var areEqual = file1 == file2; // Value equality
```

Benefits:
- **Immutability** - Thread-safe, predictable
- **Value Equality** - Two DTOs with same data are equal
- **With-Expressions** - Easy to create modified copies
- **Pattern Matching** - Clean deconstruction
- **ToString()** - Built-in readable output for logging

## Mapping: Entity ↔ DTO

### Option 1: Manual Mapping (Recommended for control)
```csharp
public static FileResponse ToDto(this FileEntity entity) => new(
    entity.Id,
    entity.TenantId,
    entity.OwnerId,
    entity.FolderId,
    entity.OriginalName,
    entity.MimeType,
    entity.SizeBytes,
    entity.S3Key,
    entity.IsDeleted,
    entity.DeletedAt,
    entity.CreatedAt,
    entity.UpdatedAt
);
```

### Option 2: AutoMapper (For complex scenarios)
```csharp
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<FileEntity, FileResponse>();
        CreateMap<UploadUrlRequest, FileEntity>();
    }
}
```

## Anti-Patterns to Avoid

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| Exposing Entities directly | Leaks DB schema, security risk | Always use DTOs |
| One DTO for everything | Over-fetching, tight coupling | Purpose-specific DTOs |
| DTOs with behavior | Violates DTO definition | Keep DTOs as data-only |
| Missing validation attributes | Invalid data reaches business logic | Annotate request DTOs |