# File Repository

## Interface: `IFileRepository`

**Location:** `Data/Interfaces/IFileRepository.cs`

```csharp
public interface IFileRepository
{
    Task<FileEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileEntity>> GetByTenantIdAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileEntity>> GetByFolderIdAsync(Guid folderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileEntity>> GetRootFilesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<long> GetTotalCountByTenantIdAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<FileEntity> CreateAsync(FileEntity file, CancellationToken cancellationToken = default);
    Task<FileEntity> UpdateAsync(FileEntity file, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByS3KeyAsync(string s3Key, CancellationToken cancellationToken = default);
}
```

### Method Details

| Method | Purpose | MVP Endpoint |
|--------|---------|--------------|
| `GetByIdAsync` | Single file by ID + tenant | `GET /files/{id}`, download URL, rename |
| `GetByTenantIdAsync` | Paginated, searchable file list | `GET /files` (main listing) |
| `GetByFolderIdAsync` | Files in specific folder | `GET /files?folder_id={id}` |
| `GetRootFilesAsync` | Files with no folder (root) | `GET /files?folder_id=null` |
| `GetTotalCountByTenantIdAsync` | Total count for pagination UI | `GET /files` (pagination metadata) |
| `GetTotalSizeByTenantIdAsync` | Live quota calculation (SUM) | `GET /tenants/usage`, pre-upload validation |
| `CreateAsync` | Register file metadata post-S3 upload | `POST /files/confirm` |
| `UpdateAsync` | Rename, move (change FolderId) | `PATCH /files/{id}` |
| `SoftDeleteAsync` | Logical delete (IsDeleted = true) | `DELETE /files/{id}` |
| `ExistsByIdAndTenantAsync` | Ownership/access validation | Before any file operation |
| `ExistsByS3KeyAsync` | S3 key uniqueness check | `POST /files/confirm` (idempotency) |

---

## Implementation: `FileRepository`

**Location:** `Data/Repositories/FileRepository.cs`

```csharp
public class FileRepository : BaseRepository<FileEntity>, IFileRepository
{
    public FileRepository(AppDbContext context) : base(context) { }

    public async Task<FileEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(f => f.TenantId == tenantId)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<FileEntity>> GetByTenantIdAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = DbSet.Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(f => f.OriginalName.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileEntity>> GetByFolderIdAsync(Guid folderId, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(f => f.TenantId == tenantId && f.FolderId == folderId)
            .OrderBy(f => f.OriginalName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileEntity>> GetRootFilesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(f => f.TenantId == tenantId && f.FolderId == null)
            .OrderBy(f => f.OriginalName)
            .ToListAsync(ct);
    }

    public async Task<long> GetTotalCountByTenantIdAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = DbSet.Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(f => f.OriginalName.Contains(searchTerm));
        }

        return await query.LongCountAsync(ct);
    }

    public async Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(f => f.TenantId == tenantId)
            .SumAsync(f => f.SizeBytes, ct);
    }

    public override async Task<FileEntity> CreateAsync(FileEntity file, CancellationToken ct = default)
    {
        await DbSet.AddAsync(file, ct);
        await Context.SaveChangesAsync(ct);
        return file;
    }

    public override async Task<FileEntity> UpdateAsync(FileEntity file, CancellationToken ct = default)
    {
        DbSet.Update(file);
        await Context.SaveChangesAsync(ct);
        return file;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var file = await GetByIdAsync(id, tenantId, ct);
        if (file == null)
            return false;

        file.IsDeleted = true;
        file.DeletedAt = DateTimeOffset.UtcNow;
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(f => f.Id == id && f.TenantId == tenantId, ct);
    }

    public async Task<bool> ExistsByS3KeyAsync(string s3Key, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(f => f.S3Key == s3Key, ct);
    }
}
```

---

## Key Implementation Notes

### 1. Global Query Filter Handles Soft Delete

`AppDbContext` configures:
```csharp
entity.HasQueryFilter(e => !e.IsDeleted);
```

All `DbSet` queries **automatically exclude** `IsDeleted = true` files.
- `GetByIdAsync`, `GetByTenantIdAsync`, etc. only return active files
- `SoftDeleteAsync` sets `IsDeleted = true` + `DeletedAt = now()` — filter handles the rest
- To include deleted: `Context.Files.IgnoreQueryFilters().Where(...)`

### 2. Quota Calculation: Live SUM() (Decision #4)

```csharp
public async Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
{
    return await DbSet
        .Where(f => f.TenantId == tenantId)
        .SumAsync(f => f.SizeBytes, ct);
}
```

- **No cached counter** — avoids desync bugs
- Optimized by partial index: `idx_files_tenant_active` (`WHERE is_deleted = false`)
- Called before every upload to enforce 1 GB limit

### 3. S3 Key Uniqueness

```csharp
public async Task<bool> ExistsByS3KeyAsync(string s3Key, CancellationToken ct = default)
{
    return await DbSet.AnyAsync(f => f.S3Key == s3Key, ct);
}
```

- **DB**: Unique index `uq_files_s3_key`
- **App**: Check before `CreateAsync` (idempotency for retry logic)

### 4. Search Implementation

Simple `Contains` on `OriginalName`:
```csharp
if (!string.IsNullOrWhiteSpace(searchTerm))
{
    query = query.Where(f => f.OriginalName.Contains(searchTerm));
}
```

Case-insensitive in PostgreSQL (default collation). For full-text search later, add `tsvector` column.

### 5. DB Trigger Validates Tenant Consistency

From [Database Schema](../database-schema.md#files):
```sql
-- Trigger: trg_files_validate_tenant
-- Ensures file.owner_id AND file.folder_id (if set) belong to file.tenant_id
```

Repository doesn't duplicate — service layer catches `DbUpdateException`.

---

## Usage Examples

```csharp
// Confirm upload (client calls after S3 PUT succeeds)
public async Task<FileEntity> ConfirmUploadAsync(Guid tenantId, Guid userId, UploadConfirmRequest req)
{
    // 1. Validate quota
    var usage = await _files.GetTotalSizeByTenantIdAsync(tenantId);
    var tenant = await _tenants.GetByIdAsync(tenantId);
    if (usage + req.SizeBytes > tenant.StorageQuotaBytes)
        throw new QuotaExceededException("1 GB limit reached");

    // 2. Validate S3 key not already registered (idempotency)
    if (await _files.ExistsByS3KeyAsync(req.S3Key))
        throw new ConflictException("File already registered");

    // 3. Validate folder belongs to tenant (if provided)
    if (req.FolderId.HasValue)
    {
        var valid = await _folders.ExistsByIdAndTenantAsync(req.FolderId.Value, tenantId);
        if (!valid) throw new ForbiddenException("Invalid folder");
    }

    // 4. Create metadata record
    var file = new FileEntity
    {
        TenantId = tenantId,
        OwnerId = userId,
        FolderId = req.FolderId,
        OriginalName = req.OriginalName,
        MimeType = req.MimeType,
        SizeBytes = req.SizeBytes,
        S3Key = req.S3Key
    };
    return await _files.CreateAsync(file);
}

// List files with pagination + search
public async Task<PagedResult<FileEntity>> ListFilesAsync(Guid tenantId, int page, int pageSize, string? search)
{
    var files = await _files.GetByTenantIdAsync(tenantId, page, pageSize, search);
    var total = await _files.GetTotalCountByTenantIdAsync(tenantId, search);
    return new PagedResult<FileEntity>(files, total, page, pageSize);
}

// Rename or move file
public async Task<FileEntity> UpdateFileAsync(Guid fileId, Guid tenantId, UpdateFileRequest req)
{
    var file = await _files.GetByIdAsync(fileId, tenantId);
    if (file == null) throw new NotFoundException("File not found");

    if (req.NewName != null) file.OriginalName = req.NewName;
    if (req.NewFolderId != null)
    {
        var valid = await _folders.ExistsByIdAndTenantAsync(req.NewFolderId.Value, tenantId);
        if (!valid) throw new ForbiddenException("Invalid folder");
        file.FolderId = req.NewFolderId;
    }

    return await _files.UpdateAsync(file);
}

// Soft delete (move to trash)
public async Task<bool> DeleteFileAsync(Guid fileId, Guid tenantId)
{
    return await _files.SoftDeleteAsync(fileId, tenantId);
}

// Get download URL - validate access first
public async Task<FileEntity?> GetFileForDownloadAsync(Guid fileId, Guid tenantId)
{
    return await _files.GetByIdAsync(fileId, tenantId);
}
```

---

## Mapping to Database

| Property | Column | Notes |
|----------|--------|-------|
| `Id` | `id` | UUID, `gen_random_uuid()` |
| `TenantId` | `tenant_id` | Required, FK → tenants (CASCADE) |
| `OwnerId` | `owner_id` | Required, FK → users (RESTRICT) |
| `FolderId` | `folder_id` | NULL allowed, FK → folders (SET NULL) |
| `OriginalName` | `original_name` | Required, max 255 |
| `MimeType` | `mime_type` | Required, max 100 |
| `SizeBytes` | `size_bytes` | Required, CHECK >= 0 |
| `S3Key` | `s3_key` | Required, unique, max 500 |
| `IsDeleted` | `is_deleted` | Default `false`, global query filter |
| `DeletedAt` | `deleted_at` | NULL when not deleted |
| `CreatedAt` | `created_at` | `now()` default |
| `UpdatedAt` | `updated_at` | Trigger-maintained |

**Constraints:**
- `uq_files_s3_key`: Unique S3 key
- `chk_files_size_positive`: Size >= 0
- `chk_files_deleted_at`: `is_deleted = false → deleted_at NULL`; `is_deleted = true → deleted_at NOT NULL`

**Indexes:**
- `idx_files_tenant_id`
- `idx_files_owner_id`
- `idx_files_folder_id`
- `idx_files_tenant_active` (partial: `WHERE is_deleted = false`)

**Trigger:** `trg_files_validate_tenant` (owner + folder belong to same tenant)

See [Database Schema](../database-schema.md#files) and [Entity Models](../entity-models.md#fileentity).