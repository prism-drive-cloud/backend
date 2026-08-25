# Folder Repository

## Interface: `IFolderRepository`

**Location:** `Data/Interfaces/IFolderRepository.cs`

```csharp
public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Folder>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default);
    Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
```

### Method Details

| Method | Purpose | MVP Endpoint |
|--------|---------|--------------|
| `GetByIdAsync` | Single folder by ID + tenant | `GET /files/{id}` (if folder), move validation |
| `GetByTenantIdAsync` | All folders in tenant (flat) | `GET /files` (folder listing) |
| `GetRootFoldersAsync` | Alias for `GetByTenantIdAsync` (MVP) | `GET /files?folder_id=null` |
| `CreateAsync` | Create new folder | `POST /folders` |
| `UpdateAsync` | Rename folder | `PATCH /files/{id}` (if folder) |
| `DeleteAsync` | Hard delete folder | `DELETE /files/{id}` (if folder) |
| `ExistsByIdAndTenantAsync` | Ownership validation | Before file move/create |

---

## Implementation: `FolderRepository`

**Location:** `Data/Repositories/FolderRepository.cs`

```csharp
public class FolderRepository : BaseRepository<Folder>, IFolderRepository
{
    public FolderRepository(AppDbContext context) : base(context) { }

    public async Task<Folder?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<Folder>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(f => f.TenantId == tenantId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await GetByTenantIdAsync(tenantId, ct);
    }

    public override async Task<Folder> CreateAsync(Folder folder, CancellationToken ct = default)
    {
        await DbSet.AddAsync(folder, ct);
        await Context.SaveChangesAsync(ct);
        return folder;
    }

    public override async Task<Folder> UpdateAsync(Folder folder, CancellationToken ct = default)
    {
        DbSet.Update(folder);
        await Context.SaveChangesAsync(ct);
        return folder;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var folder = await GetByIdAsync(id, tenantId, ct);
        if (folder == null)
            return false;

        DbSet.Remove(folder);
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(f => f.Id == id && f.TenantId == tenantId, ct);
    }
}
```

---

## Key Implementation Notes

### 1. Flat Structure (MVP Decision #2)

Per [Decision #2](../decisions.md): **No `parent_folder_id` in MVP.**

```csharp
// All folders at same level — GetRootFoldersAsync == GetByTenantIdAsync
public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid tenantId, CancellationToken ct = default)
{
    return await GetByTenantIdAsync(tenantId, ct);
}
```

**Future Migration Path** (additive, non-breaking):
```sql
ALTER TABLE folders ADD COLUMN parent_folder_id UUID
    REFERENCES folders(id) ON DELETE CASCADE;
CREATE INDEX idx_folders_parent_id ON folders(parent_folder_id);
```
Then add `GetChildrenAsync(parentId, tenantId)` to interface.

### 2. Hard Delete (Not Soft Delete)

Folders use **hard delete** (unlike files). Rationale:
- MVP doesn't require folder recycle bin
- Cascade delete to files handled by DB trigger (`fk_files_folder_id` → `SET NULL`)
- Simpler than soft delete + filter logic

### 3. DB Trigger Validates Tenant Consistency

From [Database Schema](../database-schema.md#folders):
```sql
-- Trigger: trg_folders_validate_tenant
-- Ensures folder.owner_id belongs to folder.tenant_id
```

Repository doesn't duplicate this — catches `DbUpdateException` in service layer.

### 4. OwnerId Required

Every folder has an `OwnerId` (FK → users, `ON DELETE RESTRICT`). Service layer sets this from JWT `user_id`.

---

## Usage Examples

```csharp
// Create folder
public async Task<Folder> CreateFolderAsync(Guid tenantId, Guid userId, string name)
{
    var folder = new Folder
    {
        TenantId = tenantId,
        OwnerId = userId,
        Name = name
    };
    return await _folders.CreateAsync(folder);
}

// List all folders for tenant (flat)
public async Task<IReadOnlyList<Folder>> ListFoldersAsync(Guid tenantId)
{
    return await _folders.GetByTenantIdAsync(tenantId);
}

// Rename folder
public async Task<Folder> RenameFolderAsync(Guid folderId, Guid tenantId, string newName)
{
    var folder = await _folders.GetByIdAsync(folderId, tenantId);
    if (folder == null) throw new NotFoundException("Folder not found");

    folder.Name = newName;
    return await _folders.UpdateAsync(folder);
}

// Delete folder (files in it get FolderId = NULL via DB FK)
public async Task<bool> DeleteFolderAsync(Guid folderId, Guid tenantId)
{
    return await _folders.DeleteAsync(folderId, tenantId);
}

// Validate folder belongs to tenant before moving file into it
public async Task<bool> ValidateFolderAccessAsync(Guid folderId, Guid tenantId)
{
    return await _folders.ExistsByIdAndTenantAsync(folderId, tenantId);
}
```

---

## Mapping to Database

| Property | Column | Notes |
|----------|--------|-------|
| `Id` | `id` | UUID, `gen_random_uuid()` |
| `TenantId` | `tenant_id` | Required, FK → tenants (CASCADE) |
| `OwnerId` | `owner_id` | Required, FK → users (RESTRICT) |
| `Name` | `name` | Required, max 255 |
| `CreatedAt` | `created_at` | `now()` default |
| `UpdatedAt` | `updated_at` | Trigger-maintained |

**Indexes:**
- `idx_folders_tenant_id`
- `idx_folders_owner_id`

**Trigger:** `trg_folders_validate_tenant` (owner belongs to same tenant)

See [Database Schema](../database-schema.md#folders) and [Entity Models](../entity-models.md#folder).