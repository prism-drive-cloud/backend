# AppDbContext Configuration

Location: `Data/AppDbContext.cs`

## DbContext Definition

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<FileEntity> Files { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBaseEntity(modelBuilder);
        ConfigureTenant(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureFolder(modelBuilder);
        ConfigureFile(modelBuilder);
    }
    // ... private configuration methods
}
```

## Configuration Methods

### ConfigureBaseEntity (applies to all entities)
Reflects over all entity types implementing `BaseEntity` and configures:
- `Id` → `DEFAULT gen_random_uuid()`
- `CreatedAt` → `DEFAULT now()`, `ValueGeneratedOnAdd`
- `UpdatedAt` → `DEFAULT now()`, `ValueGeneratedOnAddOrUpdate`

**Note**: `UpdatedAt` is actually maintained by PostgreSQL triggers (`trigger_set_updated_at()`), not EF Core. The `ValueGeneratedOnAddOrUpdate` tells EF to re-read the value after save.

---

### ConfigureTenant
- Table: `tenants`
- PK: `id`
- Column mapping: PascalCase properties → snake_case columns
- Unique index: `Slug` → `uq_tenants_slug`
- Defaults: `is_personal = false`, `storage_quota_bytes = 1073741824`

---

### ConfigureUser
- Table: `users`
- PK: `id`
- `TenantId` nullable (for SuperAdmin)
- `Role` → string conversion (`HasConversion<string>()`)
- Unique index: `Email` → `uq_users_email`
- Index: `TenantId` → `idx_users_tenant_id`
- Defaults: `is_active = true`
- Check constraints: enforced at DB level (role enum, super_admin tenant_id rule)

---

### ConfigureFolder
- Table: `folders`
- PK: `id`
- Required FKs:
  - `TenantId` → `tenants(id)` ON DELETE CASCADE (`fk_folders_tenant_id`)
  - `OwnerId` → `users(id)` ON DELETE RESTRICT (`fk_folders_owner_id`)
- Indexes on both FKs
- No navigation properties

---

### ConfigureFile
- Table: `files`
- PK: `id`
- FKs:
  - `TenantId` → `tenants(id)` ON DELETE CASCADE (`fk_files_tenant_id`)
  - `OwnerId` → `users(id)` ON DELETE RESTRICT (`fk_files_owner_id`)
  - `FolderId` → `folders(id)` ON DELETE SET NULL (`fk_files_folder_id`)
- Unique index: `S3Key` → `uq_files_s3_key`
- Indexes: `TenantId`, `OwnerId`, `FolderId`
- **Global Query Filter**: `HasQueryFilter(e => !e.IsDeleted)`
  - Automatically excludes soft-deleted files from all LINQ queries
  - Use `.IgnoreQueryFilters()` to include deleted files if needed
- Check constraints: DB-level (size >= 0, deleted_at consistency)

---

## Registration (Program.cs)

```csharp
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**Connection String** (appsettings.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=minidirve;Username=minidrive_user;Password=minidrive_pass;"
  }
}
```

---

## Key Design Decisions

| Decision | Implementation |
|----------|----------------|
| No navigation properties | Per requirement; FK relationships configured but no `ICollection<>` properties |
| Fluent API over Data Annotations | Centralized in `OnModelCreating`, better for complex config |
| BaseEntity reflection | DRY configuration for common columns |
| Soft delete via global filter | `FileEntity.IsDeleted` filtered out automatically |
| Enum → string conversion | `UserRole` stored as readable TEXT in DB |
| snake_case column mapping | Matches PostgreSQL convention |
| DB triggers for UpdatedAt | `trigger_set_updated_at()` function; EF reads back after save |
| Multi-tenant isolation | DB triggers enforce; app layer should also validate |

---

## Next Steps

1. **Migrations**: Run `dotnet ef migrations add InitialCreate` then `dotnet ef database update`
2. **Seeding**: Add seed data in `OnModelCreating` or separate seeder
3. **Repository/Service layer**: Implement data access patterns
4. **Tenant context**: Add `ITenantContext` for current tenant resolution in multi-tenant queries