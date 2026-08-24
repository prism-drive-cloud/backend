# Base Repository

## Class: `BaseRepository<T>`

**Location:** `Data/Repositories/BaseRepository.cs`

```csharp
public abstract class BaseRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    protected BaseRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return false;

        DbSet.Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    protected IQueryable<T> ApplyTenantFilter(IQueryable<T> query, Guid tenantId)
    {
        return query.Where(e => EF.Property<Guid>(e, "TenantId") == tenantId);
    }
}
```

---

## Purpose

Provides **shared CRUD boilerplate** for all repositories while allowing entity-specific overrides.

### Inheritance Hierarchy

```
BaseRepository<T> (abstract)
    ├── TenantRepository : BaseRepository<Tenant>
    ├── UserRepository : BaseRepository<User>
    ├── FolderRepository : BaseRepository<Folder>
    └── FileRepository : BaseRepository<FileEntity>
```

---

## Method Details

### `GetByIdAsync(Guid id, CancellationToken)`

**Default:** Simple PK lookup, **no tenant filter**.

```csharp
public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    return await DbSet.FirstOrDefaultAsync(e => e.Id == id, ct);
}
```

**Overridden by:**
- `TenantRepository` — same (tenant is root)
- `UserRepository` — same (global lookup for login)
- `FolderRepository` — **adds tenant filter**
- `FileRepository` — **adds tenant filter**

### `CreateAsync(T entity, CancellationToken)`

**Default:** Insert + SaveChanges.

```csharp
public virtual async Task<T> CreateAsync(T entity, CancellationToken ct = default)
{
    await DbSet.AddAsync(entity, ct);
    await Context.SaveChangesAsync(ct);
    return entity;
}
```

**Overridden by:** All concrete repositories (to satisfy interface contracts with explicit signatures).

### `UpdateAsync(T entity, CancellationToken)`

**Default:** Attach as modified + SaveChanges.

```csharp
public virtual async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
{
    DbSet.Update(entity);
    await Context.SaveChangesAsync(ct);
    return entity;
}
```

**Overridden by:** All concrete repositories.

### `DeleteAsync(Guid id, CancellationToken)`

**Default:** Find by ID → Remove → SaveChanges.

```csharp
public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
{
    var entity = await GetByIdAsync(id, ct);
    if (entity == null) return false;

    DbSet.Remove(entity);
    await Context.SaveChangesAsync(ct);
    return true;
}
```

**Used by:** `FolderRepository` (hard delete), `TenantRepository` (not exposed in interface).

**NOT used by:** `FileRepository` (uses `SoftDeleteAsync` instead), `UserRepository` (not exposed).

### `ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken)`

**Default:** Generic existence check.

```csharp
public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
{
    return await DbSet.AnyAsync(predicate, ct);
}
```

**Usage:** Base for specific `ExistsBy...` methods in concrete repositories.

### `ApplyTenantFilter(IQueryable<T>, Guid tenantId)`

**Helper** for building tenant-scoped queries using reflection:

```csharp
protected IQueryable<T> ApplyTenantFilter(IQueryable<T> query, Guid tenantId)
{
    return query.Where(e => EF.Property<Guid>(e, "TenantId") == tenantId);
}
```

**Example usage in concrete repository:**
```csharp
public async Task<IReadOnlyList<Folder>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
{
    return await ApplyTenantFilter(DbSet, tenantId)
        .OrderBy(f => f.Name)
        .ToListAsync(ct);
}
```

---

## Design Notes

### Why Not Generic `IRepository<T>`?

| Generic `IRepository<T>` | Specialized Interfaces (Current) |
|--------------------------|----------------------------------|
| Forces all entities to have same methods | Each entity exposes only what it needs |
| Leaks `IQueryable` or requires specification pattern | Clean method signatures matching endpoints |
| Harder to document and test | Explicit contracts per entity |
| `GetById` ambiguous: tenant filter or not? | `GetByIdAsync(id)` vs `GetByIdAsync(id, tenantId)` |

### Virtual Methods Enable Override

All CRUD methods are `virtual` so concrete repositories can:
1. Add tenant filters (`FolderRepository`, `FileRepository`)
2. Change behavior (`FileRepository` uses soft delete)
3. Satisfy interface contracts with exact signatures

### Constraint: `where T : BaseEntity`

Ensures all entities have:
- `Guid Id`
- `DateTimeOffset CreatedAt`
- `DateTimeOffset UpdatedAt`

This enables the base `GetByIdAsync` and timestamp handling.

---

## Testing

Mock `BaseRepository<T>` indirectly by mocking the concrete interface:

```csharp
// Don't mock BaseRepository directly
var mockFiles = new Mock<IFileRepository>();
mockFiles.Setup(x => x.GetByIdAsync(id, tenantId, It.IsAny<CancellationToken>()))
         .ReturnsAsync(expectedFile);
```

Integration tests use real `AppDbContext` with test database — `BaseRepository` methods execute real SQL.

---

## Future Extensions

| Feature | Implementation Approach |
|---------|------------------------|
| **Bulk operations** | Add `CreateRangeAsync`, `UpdateRangeAsync` to base |
| **Audit logging** | Decorator pattern wrapping repositories |
| **Read replicas** | Add `GetByIdAsync` overload with `bool useReadReplica` |
| **Caching** | `CachedRepository<T>` decorator with `IMemoryCache` |