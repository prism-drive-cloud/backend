# Dependency Injection Registration

## Extension Method: `AddDataAccess()`

**Location:** `Data/DAORegistration.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Data.Repositories;

namespace miniDriveBackend.Data
{
    public static class DAORegistration
    {
        public static IServiceCollection AddDataAccess(this IServiceCollection services)
        {
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IFolderRepository, FolderRepository>();
            services.AddScoped<IFileRepository, FileRepository>();
            return services;
        }
    }
}
```

---

## Registration in Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Register all DAO repositories
builder.Services.AddDataAccess();

var app = builder.Build();
// ...
```

---

## Lifetime: Scoped

All repositories registered as **`AddScoped`**:

| Lifetime | Behavior | Why Scoped |
|----------|----------|------------|
| **Scoped** | New instance per HTTP request | Shares `AppDbContext` (also Scoped) within request |
| Transient | New instance every injection | Would create multiple `DbContext` instances per request |
| Singleton | Single instance app lifetime | `DbContext` not thread-safe; would cause concurrency bugs |

**Result:** One `AppDbContext` + four repositories per request, all sharing the same EF Core change tracker.

---

## Dependency Graph

```
HTTP Request
    │
    ▼
┌─────────────────────────────────────┐
│         AppDbContext (Scoped)       │
│  ┌─────────────────────────────┐    │
│  │ Change Tracker              │    │
│  │ DbSet<Tenant>               │    │
│  │ DbSet<User>                 │    │
│  │ DbSet<Folder>               │    │
│  │ DbSet<FileEntity>           │    │
│  └─────────────────────────────┘    │
└─────────────────────────────────────┘
    │              │              │              │
    ▼              ▼              ▼              ▼
TenantRepo    UserRepo      FolderRepo     FileRepo
(Scoped)      (Scoped)      (Scoped)       (Scoped)
    │              │              │              │
    └──────────────┴──────────────┴──────────────┘
                       │
                       ▼
              Service Layer
              (Scoped)
```

---

## Injection in Services

```csharp
public class FileService
{
    private readonly IFileRepository _files;
    private readonly ITenantRepository _tenants;
    private readonly IFolderRepository _folders;

    public FileService(
        IFileRepository files,
        ITenantRepository tenants,
        IFolderRepository folders)
    {
        _files = files;
        _tenants = tenants;
        _folders = folders;
    }

    public async Task<FileEntity> UploadAsync(Guid tenantId, Guid userId, UploadConfirmRequest req)
    {
        // All repositories share same DbContext instance
        var usage = await _tenants.GetUsageAsync(tenantId);
        var folderValid = req.FolderId == null || await _folders.ExistsByIdAndTenantAsync(req.FolderId.Value, tenantId);
        
        // ... validation logic
        
        var file = new FileEntity { /* ... */ };
        return await _files.CreateAsync(file); // Single SaveChanges commits all
    }
}
```

---

## Testing with DI

### Unit Tests: Mock Interfaces

```csharp
var services = new ServiceCollection();
services.AddScoped(_ => Mock.Of<IFileRepository>());
services.AddScoped(_ => Mock.Of<ITenantRepository>());
// ...
var provider = services.BuildServiceProvider();
```

### Integration Tests: Real Implementation

```csharp
var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services =>
        {
            // Replace with test database
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(testConnectionString));
        });
    });

var scope = factory.Services.CreateScope();
var files = scope.ServiceProvider.GetRequiredService<IFileRepository>();
var result = await files.GetByIdAsync(fileId, tenantId);
```

---

## Adding New Repositories

1. Create interface in `Data/Interfaces/`
2. Create implementation in `Data/Repositories/`
3. Add one line to `AddDataAccess()`:

```csharp
public static IServiceCollection AddDataAccess(this IServiceCollection services)
{
    services.AddScoped<ITenantRepository, TenantRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IFolderRepository, FolderRepository>();
    services.AddScoped<IFileRepository, FileRepository>();
    services.AddScoped<IShareRepository, ShareRepository>();  // NEW
    services.AddScoped<IVaultRepository, VaultRepository>();  // NEW
    return services;
}
```

No changes to `Program.cs` or service constructors needed (just add new parameter).

---

## Common Pitfalls

| Pitfall | Symptom | Fix |
|---------|---------|-----|
| Forgetting `AddDataAccess()` | `Unable to resolve service for type 'IFileRepository'` | Call `builder.Services.AddDataAccess()` after `AddDbContext` |
| Registering as Transient | `Cannot access a disposed DbContext` | Use `AddScoped` (matches `DbContext` lifetime) |
| Registering as Singleton | `DbContext` concurrency exceptions | Use `AddScoped` |
| Multiple `AddDbContext` calls | Multiple `DbContext` instances per request | Call `AddDbContext` once, then `AddDataAccess()` |
| Injecting `AppDbContext` directly in services | Tight coupling, hard to test | Inject repository interfaces instead |