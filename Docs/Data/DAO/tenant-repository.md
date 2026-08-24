# Tenant Repository

## Interface: `ITenantRepository`

**Location:** `Data/Interfaces/ITenantRepository.cs`

```csharp
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task<long> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
```

### Method Details

| Method | Purpose | MVP Endpoint |
|--------|---------|--------------|
| `GetByIdAsync` | Retrieve tenant by UUID | `GET /auth/me` (tenant info) |
| `GetBySlugAsync` | Retrieve tenant by unique slug | `POST /auth/register-tenant` (slug uniqueness) |
| `CreateAsync` | Insert new tenant | `POST /auth/register-tenant` |
| `GetUsageAsync` | Calculate storage usage (live SUM) | `GET /tenants/usage` |
| `ExistsBySlugAsync` | Check slug availability | `POST /auth/register-tenant` (validation) |

---

## Implementation: `TenantRepository`

**Location:** `Data/Repositories/TenantRepository.cs`

```csharp
public class TenantRepository : BaseRepository<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext context) : base(context) { }

    public override async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public override async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default)
    {
        await DbSet.AddAsync(tenant, ct);
        await Context.SaveChangesAsync(ct);
        return tenant;
    }

    public async Task<long> GetUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await Context.Files
            .Where(f => f.TenantId == tenantId && !f.IsDeleted)
            .SumAsync(f => f.SizeBytes, ct);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(t => t.Slug == slug, ct);
    }
}
```

---

## Key Implementation Notes

### 1. No Tenant Filter on Tenant Queries
Tenant is the isolation root — queries don't filter by `tenantId` (would be circular).

### 2. Quota Calculation Delegates to Files
`GetUsageAsync` uses `Context.Files` directly (not `DbSet`) because:
- Usage is sum of **files**, not a tenant property
- Leverages partial index `idx_files_tenant_active` (`WHERE is_deleted = false`)
- Matches [Decision #4](../decisions.md): live `SUM()` over cached counter

### 3. Slug Uniqueness Enforced at DB + App Level
- **DB**: Unique index `uq_tenants_slug`
- **App**: `ExistsBySlugAsync` check before insert (better UX than catching DbUpdateException)

### 4. SuperAdmin Access
SuperAdmin uses same methods — no special repository methods needed. Service layer handles authorization.

---

## Usage Examples

```csharp
// Register new tenant (corporate or personal)
public async Task<Tenant> RegisterTenantAsync(string name, string slug, bool isPersonal)
{
    if (await _tenants.ExistsBySlugAsync(slug))
        throw new ConflictException("Slug already taken");

    var tenant = new Tenant
    {
        Name = name,
        Slug = slug,
        IsPersonal = isPersonal,
        StorageQuotaBytes = 1_073_741_824 // 1 GB
    };

    return await _tenants.CreateAsync(tenant);
}

// Check quota before file upload
public async Task<bool> CanUploadAsync(Guid tenantId, long fileSizeBytes)
{
    var usage = await _tenants.GetUsageAsync(tenantId);
    var tenant = await _tenants.GetByIdAsync(tenantId);
    return usage + fileSizeBytes <= tenant.StorageQuotaBytes;
}

// Get tenant for authenticated user
public async Task<Tenant?> GetCurrentTenantAsync(Guid tenantId)
{
    return await _tenants.GetByIdAsync(tenantId);
}
```

---

## Mapping to Database

| Property | Column | Notes |
|----------|--------|-------|
| `Id` | `id` | UUID, `gen_random_uuid()` |
| `Name` | `name` | Required, max 255 |
| `Slug` | `slug` | Required, unique, max 100 |
| `IsPersonal` | `is_personal` | Default `false` |
| `StorageQuotaBytes` | `storage_quota_bytes` | Default 1 GB |
| `CreatedAt` | `created_at` | `now()` default |
| `UpdatedAt` | `updated_at` | Trigger-maintained |

See [Database Schema](../database-schema.md#tenants) and [Entity Models](../entity-models.md#tenant).