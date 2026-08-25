# Interfaces - Purpose and Design

## What is an Interface?

An **Interface** in C# defines a contract that implementing classes must fulfill. It specifies *what* operations are available without dictating *how* they are implemented.

```csharp
public interface IFileService
{
    Task<FileResponse> GetFileByIdAsync(Guid fileId, Guid tenantId, CancellationToken ct = default);
    // ... other methods
}
```

## Why Use Interfaces Here?

### 1. **Testability**
- Controllers can be unit tested with mock implementations
- Business logic can be tested in isolation from external dependencies (S3, Database)
- Enables TDD (Test-Driven Development)

### 2. **Dependency Inversion (SOLID)**
- High-level modules (Controllers) don't depend on low-level modules (EF Core, AWS SDK)
- Both depend on abstractions (Interfaces)
- Easy to swap implementations (e.g., S3 → Azure Blob, PostgreSQL → SQL Server)

### 3. **Decoupling**
- Presentation layer (API) doesn't know about data access details
- Business layer doesn't know about HTTP, serialization, or framework specifics
- Each layer can evolve independently

### 4. **Multi-Tenancy Enforcement**
- All interfaces accept `tenantId` as explicit parameter
- Prevents accidental cross-tenant data access
- Centralizes authorization logic in one place

## Interface Catalog

| Interface | Responsibility | Key Design Decision |
|-----------|----------------|---------------------|
| `ITokenService` | JWT generation/validation | Separated from Auth for single responsibility |
| `IAuthService` | Authentication flows | Handles password hashing internally |
| `ITenantService` | Tenant lifecycle & quotas | Validates slug uniqueness, quota checks |
| `IFileService` | File CRUD + S3 orchestration | Coordinates upload flow (URL → confirm) |
| `IFolderService` | Folder hierarchy management | Supports nested folders, root/sub queries |
| `IUserService` | User management within tenant | Role-based operations, activation/deactivation |
| `IStorageService` | Quota validation & tracking | Centralizes 1GB limit logic |
| `IS3Service` | AWS S3 presigned URLs | Abstracts AWS SDK, builds tenant-scoped keys |

## Naming Conventions

- **Prefix**: `I` + PascalCase (e.g., `IFileService`)
- **Async Suffix**: All methods end with `Async`
- **CancellationToken**: Last parameter, defaulted to `default`
- **TenantId**: Explicit parameter on all multi-tenant operations

## Usage in Controllers

```csharp
[ApiController]
[Route("api/v1/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles([FromQuery] FileQueryParameters parameters)
    {
        var tenantId = GetTenantIdFromClaims();
        var result = await _fileService.GetFilesAsync(tenantId, parameters);
        return Ok(result);
    }
}
```

## Registration (Dependency Injection)

```csharp
// In Business/ServiceRegistration.cs (to be created)
public static class BusinessRegistration
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IS3Service, S3Service>();
        services.AddScoped<ITokenService, TokenService>();
        return services;
    }
}
```