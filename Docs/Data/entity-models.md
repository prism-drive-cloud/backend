# Entity Models

All entities inherit from `BaseEntity` (see below).

## BaseEntity (`Models/BaseEntity.cs`)

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**EF Configuration** (applied to all derived entities via `ConfigureBaseEntity`):
- `Id`: `DEFAULT gen_random_uuid()`
- `CreatedAt`: `DEFAULT now()`, `ValueGeneratedOnAdd`
- `UpdatedAt`: `DEFAULT now()`, `ValueGeneratedOnAddOrUpdate` (DB trigger handles updates)

---

## Tenant (`Models/Tenant.cs`)

Maps to `tenants` table.

```csharp
public class Tenant : BaseEntity
{
    [Required, MaxLength(255)] public string Name { get; set; }
    [Required, MaxLength(100)] public string Slug { get; set; }
    public bool IsPersonal { get; set; } = false;
    public long StorageQuotaBytes { get; set; } = 1073741824; // 1 GB
}
```

**EF Config** (`ConfigureTenant`):
- Table: `tenants`
- Column mapping: snake_case
- Unique index on `Slug` → `uq_tenants_slug`
- Defaults match schema

---

## UserRole (`Models/UserRole.cs`)

```csharp
public enum UserRole
{
    SuperAdmin,
    TenantAdmin,
    User
}
```

**EF Config**: `HasConversion<string>()` → stores as TEXT in DB (`'super_admin'`, `'tenant_admin'`, `'user'`)

---

## User (`Models/User.cs`)

Maps to `users` table.

```csharp
public class User : BaseEntity
{
    public Guid? TenantId { get; set; }           // NULL only for SuperAdmin
    [Required, MaxLength(255)] public string Email { get; set; }
    [Required] public string PasswordHash { get; set; }
    [Required, MaxLength(255)] public string FullName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
}
```

**EF Config** (`ConfigureUser`):
- Table: `users`
- `TenantId` nullable (for super_admin)
- `Role` stored as string via conversion
- Unique index on `Email` → `uq_users_email`
- Index on `TenantId` → `idx_users_tenant_id`
- Check constraints enforced at DB level (role values, super_admin tenant_id rule)

---

## Folder (`Models/Folder.cs`)

Maps to `folders` table (flat structure, no parent_id in MVP).

```csharp
public class Folder : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OwnerId { get; set; }
    [Required, MaxLength(255)] public string Name { get; set; }
}
```

**EF Config** (`ConfigureFolder`):
- Table: `folders`
- Required FKs: `TenantId` (CASCADE), `OwnerId` (RESTRICT)
- Indexes on both FKs
- No navigation properties (per requirement)

---

## FileEntity (`Models/FileEntity.cs`)

Maps to `files` table. Renamed from `File` to avoid `System.IO.File` conflict.

```csharp
public class FileEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? FolderId { get; set; }           // NULL allowed, SET NULL on delete
    [Required, MaxLength(255)] public string OriginalName { get; set; }
    [Required, MaxLength(100)] public string MimeType { get; set; }
    public long SizeBytes { get; set; }
    [Required, MaxLength(500)] public string S3Key { get; set; }
    public bool IsDeleted { get; set; } = false;  // Soft delete
    public DateTimeOffset? DeletedAt { get; set; }
}
```

**EF Config** (`ConfigureFile`):
- Table: `files`
- FKs: `TenantId` (CASCADE), `OwnerId` (RESTRICT), `FolderId` (SET NULL)
- Unique index on `S3Key` → `uq_files_s3_key`
- Indexes on all FKs + partial index on `tenant_id WHERE is_deleted = false`
- **Global Query Filter**: `HasQueryFilter(e => !e.IsDeleted)` — soft-deleted files excluded from all queries by default
- Check constraints at DB level (size >= 0, deleted_at consistency)