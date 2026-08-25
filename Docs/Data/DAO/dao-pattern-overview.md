# DAO Pattern Overview

## Why DAO Over Direct EF Core Usage?

| Concern | Direct EF Core in Services | DAO Pattern (This Implementation) |
|---------|---------------------------|-----------------------------------|
| **Testability** | Hard to mock `DbContext` | Easy to mock interfaces |
| **Multi-Tenant Safety** | Scattered `Where(t => t.TenantId == id)` | Centralized in repository methods |
| **Query Reuse** | Duplicated LINQ across services | Single source of truth per entity |
| **Schema Changes** | Ripple through service layer | Isolated to repository implementations |
| **Separation of Concerns** | Business logic mixed with queries | Pure data access in repositories |

## Design Decisions

### 1. Specialized Interfaces (No Generic `IRepository<T>`)

Each entity has unique query requirements:

- **Tenant**: Slug lookup, quota calculation
- **User**: Email lookup, role-based queries, SuperAdmin handling
- **Folder**: Flat structure (MVP), tenant-scoped listing
- **File**: Paginated search, soft delete, S3 key uniqueness, quota aggregation

A generic interface would force unused methods on consumers and leak abstraction.

### 2. Explicit TenantId Parameter

```csharp
// Good - explicit, testable, auditable
Task<FileEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);

// Avoid - implicit via ambient context
Task<FileEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
```

**Rationale:**
- Makes tenant isolation visible in method signatures
- Enables unit testing with different tenant scenarios
- Prevents accidental cross-tenant queries
- SuperAdmin global queries use separate methods (`GetSuperAdminsAsync`)

### 3. BaseRepository<T> for DRY CRUD

```csharp
public abstract class BaseRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    public virtual async Task<T> CreateAsync(T entity, CancellationToken ct = default)
    public virtual async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)

    protected IQueryable<T> ApplyTenantFilter(IQueryable<T> query, Guid tenantId)
}
```

Concrete repositories override `GetByIdAsync` to add tenant filter, and `CreateAsync`/`UpdateAsync` for entity-specific logic.

### 4. Soft Delete Handling

Only `FileEntity` implements soft delete (per requirements). The global query filter in `AppDbContext` automatically excludes `IsDeleted = true`:

```csharp
// AppDbContext.ConfigureFile()
entity.HasQueryFilter(e => !e.IsDeleted);
```

`FileRepository.SoftDeleteAsync()` sets `IsDeleted = true` and `DeletedAt = now()`, then saves. The global filter handles the rest.

### 5. Quota Calculation: Live SUM()

Per [decisions.md](../decisions.md), quota uses live aggregation:

```csharp
public async Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
{
    return await DbSet
        .Where(f => f.TenantId == tenantId)
        .SumAsync(f => f.SizeBytes, ct);
}
```

No cached counter — avoids desync bugs. Partial index `idx_files_tenant_active` optimizes this query.

## Method Signature Conventions

| Pattern | Example | Use Case |
|---------|---------|----------|
| `GetByIdAsync(id, tenantId)` | Single entity by PK + tenant | Standard lookup |
| `GetByTenantIdAsync(tenantId)` | All entities for tenant | Listing (folders, users) |
| `GetByTenantIdAsync(tenantId, page, pageSize, search)` | Paginated, searchable | File listing with search |
| `GetTotalCountByTenantIdAsync(tenantId, search)` | Count for pagination | UI pagination controls |
| `ExistsByIdAndTenantAsync(id, tenantId)` | Boolean existence check | Validation before operations |
| `CreateAsync(entity)` | Insert new | All entities |
| `UpdateAsync(entity)` | Update existing | All entities |
| `SoftDeleteAsync(id, tenantId)` | Logical delete | Files only |
| `DeleteAsync(id, tenantId)` | Hard delete | Folders, Tenants |

## CancellationToken Propagation

All async methods accept `CancellationToken` with default value:

```csharp
public async Task<T?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
```

This enables:
- Request cancellation on client disconnect
- Timeout enforcement via middleware
- Proper resource cleanup

## Testing Strategy

### Unit Tests (Repository Logic)
Mock the interface, not `DbContext`:

```csharp
var mockFiles = new Mock<IFileRepository>();
mockFiles.Setup(x => x.GetByIdAsync(fileId, tenantId, It.IsAny<CancellationToken>()))
         .ReturnsAsync(expectedFile);
```

### Integration Tests (Real Database)
Use `WebApplicationFactory` with testcontainers (PostgreSQL):

```csharp
var scope = factory.Services.CreateScope();
var files = scope.ServiceProvider.GetRequiredService<IFileRepository>();
var result = await files.GetByIdAsync(id, tenantId);
```

## Future Extensibility

The pattern supports additive changes without breaking existing code:

| Feature | Change Required |
|---------|-----------------|
| Nested folders | Add `parent_folder_id` to schema, `GetChildrenAsync` to `IFolderRepository` |
| File sharing | New `IShareRepository`, `Share` entity |
| Vault/Strong folder | New `IVaultRepository`, `Vault` entity |
| Recycle bin | Add `GetDeletedFilesAsync` to `IFileRepository` |
| Audit logging | Decorator pattern on repositories |

Each is a new interface/implementation — existing code untouched.