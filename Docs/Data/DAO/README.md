# Data Access Object (DAO) Layer Documentation

## Overview

This folder documents the **DAO (Data Access Object) pattern** implementation for miniDriveBackend. The DAO layer provides a clean abstraction over Entity Framework Core, encapsulating all database access logic and enforcing multi-tenant isolation at the data access level.

## Structure

```
Data/
├── Interfaces/           # Repository contracts (abstractions)
│   ├── ITenantRepository.cs
│   ├── IUserRepository.cs
│   ├── IFolderRepository.cs
│   └── IFileRepository.cs
├── Repositories/         # Concrete implementations
│   ├── BaseRepository.cs     # Shared CRUD functionality
│   ├── TenantRepository.cs
│   ├── UserRepository.cs
│   ├── FolderRepository.cs
│   └── FileRepository.cs
└── DAORegistration.cs    # DI extension method
```

## Files in This Folder

| File | Description |
|------|-------------|
| [dao-pattern-overview.md](dao-pattern-overview.md) | Architecture decisions, design principles, and usage guide |
| [tenant-repository.md](tenant-repository.md) | `ITenantRepository` and `TenantRepository` documentation |
| [user-repository.md](user-repository.md) | `IUserRepository` and `UserRepository` documentation |
| [folder-repository.md](folder-repository.md) | `IFolderRepository` and `FolderRepository` documentation |
| [file-repository.md](file-repository.md) | `IFileRepository` and `FileRepository` documentation |
| [base-repository.md](base-repository.md) | `BaseRepository<T>` shared functionality |
| [di-registration.md](di-registration.md) | Dependency injection setup with `AddDataAccess()` |

## Key Principles

1. **Interface Segregation**: Each entity has its own specialized repository interface (no generic `IRepository<T>`)
2. **Multi-Tenant Isolation**: All queries filter by `tenantId` (except SuperAdmin global queries)
3. **Explicit Contracts**: Method signatures match the MVP endpoint requirements
4. **No Navigation Properties**: Entities use FKs only; repositories handle joins explicitly
5. **Service Layer Owns Transactions**: Repositories are stateless; `DbContext` lifetime is Scoped

## Quick Start

```csharp
// Program.cs
builder.Services.AddDataAccess();  // Registers all repositories

// Service layer
public class FileService
{
    private readonly IFileRepository _files;
    private readonly ITenantRepository _tenants;

    public FileService(IFileRepository files, ITenantRepository tenants)
    {
        _files = files;
        _tenants = tenants;
    }

    public async Task<FileEntity> UploadAsync(Guid tenantId, FileEntity file)
    {
        var usage = await _tenants.GetUsageAsync(tenantId);
        if (usage + file.SizeBytes > 1_073_741_824) // 1 GB
            throw new InvalidOperationException("Quota exceeded");

        return await _files.CreateAsync(file);
    }
}
```

## Related Documentation

- [Entity Models](../entity-models.md) — Domain entity definitions
- [DbContext Configuration](../dbcontext-configuration.md) — EF Core mapping details
- [Database Schema](../database-schema.md) — PostgreSQL schema and triggers
- [Architecture Decisions](../decisions.md) — Design rationale