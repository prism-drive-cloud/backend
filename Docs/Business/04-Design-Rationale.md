# Why These Specific Interfaces, DTOs, and Exceptions?

This document explains the rationale behind each interface, DTO, and exception created for the Mini Drive Business Layer, mapped to the requirements in `requisitos_minimos.md`.

---

## Interfaces - Mapped to Requirements

### `ITokenService` - JWT Management
**Requirement**: *Flujo 2: Autenticación y Acceso - "El sistema valida y emite un token JWT que incluye: user_id, tenant_id y role"*
- Separated from `IAuthService` for Single Responsibility
- Enables token refresh without re-authentication
- Supports token validation for middleware/authentication handlers

### `IAuthService` - Authentication Flows
**Requirements**:
- *Flujo 1: Registro Corporativo* - `RegisterTenantAsync`
- *Flujo 1: Registro Personal* - `RegisterUserAsync` (for personal tenants)
- *Flujo 1: Invitación/Alta de Usuarios* - `RegisterUserAsync` (for corporate users)
- *Flujo 2: Login* - `LoginAsync`
- *GET /api/v1/auth/me* - `GetCurrentUserAsync`
- Password hashing kept internal (BCrypt/Argon2) per decision

### `ITenantService` - Multi-Tenant Core
**Requirements**:
- *Modo Empresarial (Multi-Tenant)* - `CreateTenantAsync` with slug, isolated S3 prefix
- *Aislamiento Multi-Tenant Estricto* - All methods enforce `tenantId`
- *GET /api/v1/tenants/usage* - `GetUsageAsync` returns `TenantUsageResponse`
- *Control de Cuota de 1 GB* - `ValidateQuotaAsync` called before upload
- *Slug uniqueness* - `ExistsBySlugAsync` for registration validation

### `IFileService` - File Operations + S3 Flow
**Requirements**:
- *CRUD de Archivos* - Full CRUD: `GetFilesAsync`, `GetFileByIdAsync`, `RenameAsync`, `MoveAsync`, `SoftDeleteAsync`
- *Carga Drag & Drop / Móvil* - Two-step upload: `RequestUploadUrlAsync` → `ConfirmUploadAsync`
- *Almacenamiento Directo en AWS S3* - `RequestUploadUrlAsync` returns presigned URL, no binary through API
- *Control de Cuota* - Validates quota in `RequestUploadUrlAsync` via `IStorageService`
- *Descarga de Archivos* - `GetDownloadUrlAsync` returns presigned download URL
- *Sincronización Web/Móvil* - List with pagination/search supports both frontends
- *Endpoints*: `GET /api/v1/files`, `POST /api/v1/files/upload-url`, `POST /api/v1/files/confirm`, `GET /api/v1/files/{id}/download-url`, `PATCH /api/v1/files/{id}`, `DELETE /api/v1/files/{id}`

### `IFolderService` - Folder Hierarchy
**Requirements**:
- *POST /api/v1/folders* - `CreateFolderAsync`
- *Estructura de carpetas anidadas* - Supports `ParentFolderId`, `GetSubFoldersAsync`, `FolderTreeResponse`
- *Debate: ¿Carpetas anidadas desde el inicio?* - **Yes**, interfaces support it; implementation can start flat

### `IUserService` - User Management
**Requirements**:
- *Admin Corporativo administra usuarios* - `CreateUserAsync`, `GetUsersByTenantAsync`, `UpdateUserAsync`
- *Roles: SuperAdmin, TenantAdmin, User* - `UserRole` in `CreateUserRequest`/`UpdateUserRequest`
- *Activación/Desactivación* - `ActivateUserAsync`, `DeactivateUserAsync`

### `IStorageService` - Quota Enforcement
**Requirements**:
- *Control de Cuota de 1 GB* - Centralizes quota logic
- *Validación en backend* - `CheckQuotaAvailableAsync` called before presigned URL
- *GET /api/v1/tenants/usage* - `GetStorageInfoAsync` provides used/available/percentage

### `IS3Service` - AWS S3 Abstraction
**Requirements**:
- *Presigned URLs exclusivamente* - `GeneratePresignedUploadUrlAsync`, `GeneratePresignedDownloadUrlAsync`
- *Prefijos por tenant* - `BuildS3Key` enforces `/tenants/{tenantId}/...` structure
- *CORS, IAM, políticas* - Abstracted; implementation handles AWS config
- *Visualización (preview)* - `GeneratePresignedViewUrlAsync` with content-type for inline viewing

---

## DTOs - Mapped to API Contracts

### Auth DTOs (`AuthDtos.cs`)
| DTO | Endpoint | Purpose |
|-----|----------|---------|
| `LoginRequest` | `POST /api/v1/auth/login` | Email + password input |
| `RegisterTenantRequest` | `POST /api/v1/auth/register-tenant` | Tenant + admin creation |
| `RegisterUserRequest` | `POST /api/v1/auth/register-user` | Corporate user invitation / personal registration |
| `AuthResponse` | All auth endpoints | JWT pair + user + tenant info |
| `UserProfileResponse` | `GET /api/v1/auth/me` | Current user profile |
| `ChangePasswordRequest` | `POST /api/v1/auth/change-password` | Password update |

### Tenant DTOs (`TenantDtos.cs`)
| DTO | Endpoint | Purpose |
|-----|----------|---------|
| `CreateTenantRequest` | `POST /api/v1/auth/register-tenant` | Tenant creation input |
| `TenantResponse` | `GET /api/v1/auth/me`, tenant endpoints | Tenant info for UI |
| `TenantUsageResponse` | `GET /api/v1/tenants/usage` | Quota dashboard data (used, quota, %, file/folder counts) |

### File DTOs (`FileDtos.cs`)
| DTO | Endpoint | Purpose |
|-----|----------|---------|
| `FileQueryParameters` | `GET /api/v1/files` | Pagination, search, filter, sort |
| `FileResponse` | `GET /api/v1/files`, `GET /api/v1/files/{id}` | File metadata for listing/detail |
| `UploadUrlRequest` | `POST /api/v1/files/upload-url` | File info for presigned URL generation |
| `UploadUrlResponse` | `POST /api/v1/files/upload-url` | Presigned URL + headers + expiry |
| `ConfirmUploadRequest` | `POST /api/v1/files/confirm` | Client confirms S3 upload success |
| `DownloadUrlResponse` | `GET /api/v1/files/{id}/download-url` | Presigned download/view URL |
| `RenameFileRequest` | `PATCH /api/v1/files/{id}` | Rename operation |
| `MoveFileRequest` | `PATCH /api/v1/files/{id}` | Move between folders |
| `PagedResult<T>` | `GET /api/v1/files` | Standardized pagination wrapper |

### Folder DTOs (`FolderDtos.cs`)
| DTO | Endpoint | Purpose |
|-----|----------|---------|
| `CreateFolderRequest` | `POST /api/v1/folders` | Folder creation (name + optional parent) |
| `FolderResponse` | `GET /api/v1/folders`, folder endpoints | Folder metadata |
| `RenameFolderRequest` | `PATCH /api/v1/folders/{id}` | Rename operation |
| `FolderTreeResponse` | `GET /api/v1/folders/tree` | Nested structure for UI tree view |

### User DTOs (`UserDtos.cs`)
| DTO | Endpoint | Purpose |
|-----|----------|---------|
| `CreateUserRequest` | `POST /api/v1/auth/register-user` | User creation by admin |
| `UserResponse` | `GET /api/v1/users`, user endpoints | User info for admin UI |
| `UpdateUserRequest` | `PATCH /api/v1/users/{id}` | Partial user update (name, role, active) |

---

## Exceptions - Mapped to Error Scenarios

| Exception | Requirement Scenario | HTTP Code |
|-----------|---------------------|-----------|
| `QuotaExceededException` | *Control de Cuota de 1 GB - "Validación en backend para bloquear subidas que excedan el límite"* | 400 |
| `TenantNotFoundException` | Invalid tenant in JWT, slug not found on login | 404 |
| `UserNotFoundException` | User lookup by ID/email fails | 404 |
| `FileNotFoundException` | File access by ID fails (or cross-tenant) | 404 |
| `FolderNotFoundException` | Folder access by ID fails | 404 |
| `UnauthorizedAccessException` | *Aislamiento Multi-Tenant Estricto - "Cero fugas de información"* - Cross-tenant access attempt | 403 |
| `InvalidCredentialsException` | *Login - "El sistema valida y emite un token JWT"* - Wrong password | 401 |
| `DuplicateResourceException` | Unique constraint: tenant slug, user email, S3 key | 409 |
| `S3OperationException` | *Infraestructura AWS S3* - Network, permissions, config errors | 500 |

---

## Design Decisions Summary

| Decision | Rationale |
|----------|-----------|
| Separate `ITokenService` | JWT logic is cross-cutting; needed by middleware and auth service |
| Password hashing in `IAuthService` | Simpler for MVP; can extract `IPasswordHasher` later if needed |
| No `IShareService`/`IVaultService` | Phase 2 per requirements (Deseables) |
| No `IEventPublisher` | Not needed for MVP; add when analytics/audit required |
| `record` for DTOs | Immutability, value equality, with-expressions, clean serialization |
| Explicit `tenantId` on all methods | Compile-time enforcement of multi-tenancy; no implicit ambient context |
| `CancellationToken` on all async | Proper cancellation propagation for timeouts/scalability |
| Custom exceptions with `ErrorCode` | Frontend can handle errors programmatically without string parsing |
| `PagedResult<T>` generic | Consistent pagination across all list endpoints |

---

## Future Extensibility

The interfaces are designed to accommodate Phase 2 requirements without breaking changes:

- **Sharing**: Add `IShareService` with `CreateShareLinkAsync`, `GetShareByTokenAsync`
- **Vault**: Add `IVaultService` with `VerifyPinAsync`, `GetVaultFilesAsync`
- **Recycle Bin**: Add `IsDeleted` filter to `FileQueryParameters`, `RestoreAsync` to `IFileService`
- **Folder Upload**: Add `UploadFolderRequest` DTO, `RequestFolderUploadUrlAsync` to `IFileService`
- **Analytics**: Add `IAnalyticsService` implementing `GET /api/v1/analytics/overview`

---

## Service Implementation Requirements

This section details what each service implementation must provide to fulfill the interfaces.

### Common Patterns for All Services

```csharp
// Base constructor pattern
public class FileService : IFileService
{
    private readonly IFileRepository _fileRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IStorageService _storageService;
    private readonly IS3Service _s3Service;
    private readonly ILogger<FileService> _logger;

    public FileService(
        IFileRepository fileRepository,
        IFolderRepository folderRepository,
        ITenantRepository tenantRepository,
        IStorageService storageService,
        IS3Service s3Service,
        ILogger<FileService> logger)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _tenantRepository = tenantRepository;
        _storageService = storageService;
        _s3Service = s3Service;
        _logger = logger;
    }
    // ...
}
```

**Required for all services:**
- Constructor injection of dependencies (repositories, other services, logger)
- `CancellationToken` propagation to all async calls
- Structured logging (Information for operations, Warning for business errors, Error for exceptions)
- Input validation (throw `ArgumentException` for invalid inputs before repository calls)
- Tenant isolation enforcement on every repository call

---

### `AuthService` - `IAuthService`

**Dependencies:** `IUserRepository`, `ITenantRepository`, `ITokenService`, `ILogger`

**Implementation Details:**

| Method | Logic |
|--------|-------|
| `LoginAsync` | 1. Find user by email<br>2. Verify password hash (BCrypt)<br>3. Check `IsActive`<br>4. Generate JWT via `ITokenService`<br>5. Return `AuthResponse` with user + tenant |
| `RegisterTenantAsync` | 1. Validate slug uniqueness (`ITenantRepository.ExistsBySlugAsync`)<br>2. Create `Tenant` entity<br>3. Create admin `User` (TenantAdmin role)<br>4. Hash password<br>5. Save both in transaction<br>6. Generate JWT |
| `RegisterUserAsync` | 1. Validate caller is TenantAdmin/SuperAdmin<br>2. Check email uniqueness globally<br>3. Create `User` with provided role<br>4. Hash password<br>5. Save |
| `GetCurrentUserAsync` | 1. Get user by ID with tenant<br>2. Map to `UserProfileResponse` + `TenantResponse` |
| `ValidateCredentialsAsync` | Used by middleware; verify email + password without token generation |
| `ChangePasswordAsync` | 1. Verify current password<br>2. Hash new password<br>3. Update user |
| `RevokeRefreshTokenAsync` | Invalidate refresh token (store revoked tokens in Redis/DB) |

**Password Hashing:** Use `BCrypt.Net-Next` or `Argon2`:
```csharp
// In AuthService
private string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
private bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
```

---

### `TenantService` - `ITenantService`

**Dependencies:** `ITenantRepository`, `IFileRepository`, `IFolderRepository`, `ILogger`

| Method | Logic |
|--------|-------|
| `CreateTenantAsync` | 1. Check slug uniqueness<br>2. Create `Tenant` entity (set `StorageQuotaBytes = 1GB`)<br>3. Save |
| `GetTenantByIdAsync` / `GetTenantBySlugAsync` | Repository lookup + map to `TenantResponse` |
| `GetUsageAsync` | 1. Call `IFileRepository.GetTotalSizeByTenantIdAsync`<br>2. Call `IFileRepository.GetTotalCountByTenantIdAsync`<br>3. Call `IFolderRepository.GetByTenantIdAsync` for count<br>4. Compute percentage, available bytes<br>5. Return `TenantUsageResponse` |
| `ValidateQuotaAsync` | Delegate to `IStorageService.CheckQuotaAvailableAsync` |
| `ExistsBySlugAsync` | Direct repository call |

---

### `FileService` - `IFileService` (Most Complex)

**Dependencies:** `IFileRepository`, `IFolderRepository`, `ITenantRepository`, `IStorageService`, `IS3Service`, `ILogger`

| Method | Logic |
|--------|-------|
| `GetFilesAsync` | 1. Validate `FolderId` belongs to tenant (if provided)<br>2. Call `IFileRepository.GetByTenantIdAsync` or `GetByFolderIdAsync`<br>3. Get total count for pagination<br>4. Map to `FileResponse` list<br>5. Return `PagedResult<FileResponse>` |
| `GetFileByIdAsync` | 1. Repository lookup with tenantId<br>2. Throw `FileNotFoundException` if null<br>3. Map to `FileResponse` |
| `RequestUploadUrlAsync` | **Critical path**:<br>1. Validate quota via `IStorageService.CheckQuotaAvailableAsync(request.SizeBytes)`<br>2. Throw `QuotaExceededException` if insufficient<br>3. Validate folder exists (if provided)<br>4. Build S3 key: `_s3Service.BuildS3Key(tenantId, userId, request.FileName)`<br>5. Check S3 key uniqueness<br>6. Generate presigned URL via `IS3Service.GeneratePresignedUploadUrlAsync`<br>7. Reserve quota: `_storageService.ReserveQuotaAsync(tenantId, request.SizeBytes)`<br>8. Return `UploadUrlResponse` |
| `ConfirmUploadAsync` | 1. Verify S3 object exists (`IS3Service.ObjectExistsAsync`)<br>2. Create `FileEntity` with metadata<br>3. Save via `IFileRepository.CreateAsync`<br>4. Return `FileResponse` |
| `GetDownloadUrlAsync` | 1. Get file by ID (validates tenant)<br>2. Generate presigned download URL via `IS3Service.GeneratePresignedDownloadUrlAsync`<br>3. Return `DownloadUrlResponse` |
| `RenameAsync` | 1. Get file, validate tenant<br>2. Update `OriginalName`<br>3. Save via `UpdateAsync` |
| `MoveAsync` | 1. Get file, validate tenant<br>2. Validate target folder exists & belongs to tenant<br>3. Update `FolderId`<br>4. Save |
| `SoftDeleteAsync` | 1. Get file, validate tenant<br>2. Set `IsDeleted = true`, `DeletedAt = now`<br>3. Release quota: `_storageService.ReleaseQuotaAsync(tenantId, file.SizeBytes)`<br>4. Save |
| `GetTotalSizeByTenantAsync` | Delegate to `IFileRepository.GetTotalSizeByTenantIdAsync` |

**Critical: Two-Phase Upload Flow**
```
Client                          API                          S3
  |                              |                             |
  |-- POST /upload-url --------->|                             |
  |                              |-- Check quota              |
  |                              |-- Generate presigned URL   |
  |<-- UploadUrlResponse --------|                             |
  |                              |                             |
  |----------------------------->| (binary upload)            |
  |                              |                             |
  |-- POST /confirm ------------>|                             |
  |                              |-- Verify S3 object exists  |
  |                              |-- Save metadata to DB      |
  |                              |-- Release quota reservation|
  |<-- FileResponse -------------|                             |
```

---

### `FolderService` - `IFolderService`

**Dependencies:** `IFolderRepository`, `ILogger`

| Method | Logic |
|--------|-------|
| `CreateFolderAsync` | 1. Validate parent folder exists (if provided) & belongs to tenant<br>2. Create `Folder` entity<br>3. Save |
| `GetFoldersAsync` | If `parentFolderId` null → `GetRootFoldersAsync`, else → `GetSubFoldersAsync` |
| `GetFolderByIdAsync` | Repository lookup + tenant validation |
| `RenameFolderAsync` | Get folder, update name, save |
| `DeleteFolderAsync` | **Cascade decision needed**: Soft delete folder only? Move files to root? Delete recursively? For MVP: only allow delete if empty, or move files to root |
| `GetRootFoldersAsync` / `GetSubFoldersAsync` | Direct repository calls |

---

### `UserService` - `IUserService`

**Dependencies:** `IUserRepository`, `ILogger`

| Method | Logic |
|--------|-------|
| `CreateUserAsync` | 1. Validate email uniqueness globally<br>2. Hash password<br>3. Create `User` with `TenantId`, `Role`<br>4. Save |
| `GetUsersByTenantAsync` | Repository call + map to `UserResponse` |
| `GetUserByIdAsync` | Repository call with tenant validation |
| `UpdateUserAsync` | 1. Get user<br>2. Apply changes (name, role, IsActive)<br>3. **Role change rules**: TenantAdmin cannot create SuperAdmin; SuperAdmin can change any role<br>4. Save |
| `DeactivateUserAsync` / `ActivateUserAsync` | Toggle `IsActive` |
| `ExistsByEmailAsync` / `ExistsByIdAndTenantAsync` | Direct repository calls |

---

### `StorageService` - `IStorageService`

**Dependencies:** `ITenantRepository`, `IFileRepository`, `ILogger`

| Method | Logic |
|--------|-------|
| `CheckQuotaAvailableAsync` | 1. Get tenant quota (`Tenant.StorageQuotaBytes`)<br>2. Get used bytes (`IFileRepository.GetTotalSizeByTenantIdAsync`)<br>3. Return `used + requested <= quota` |
| `GetStorageUsageAsync` | Same as above, return used bytes |
| `GetStorageQuotaAsync` | Return `Tenant.StorageQuotaBytes` |
| `ReserveQuotaAsync` | **Optional for MVP**: Track reserved bytes in memory/Redis for concurrent upload protection. Simple version: no-op (quota checked at confirm). |
| `ReleaseQuotaAsync` | Counterpart to Reserve |
| `GetStorageInfoAsync` | Combine all above into `StorageInfo` record |

**Concurrency Note:** For MVP, quota check at `RequestUploadUrl` + `ConfirmUpload` is sufficient. For production, add distributed lock or reservation system.

---

### `S3Service` - `IS3Service`

**Dependencies:** `IAmazonS3` (AWS SDK), `IConfiguration` (bucket name, region), `ILogger`

| Method | Logic |
|--------|-------|
| `GeneratePresignedUploadUrlAsync` | 1. Create `GetPreSignedUrlRequest` with `Verb.PUT`<br>2. Set `BucketName`, `Key`, `ContentType`, `Expires` (15 min default)<br>3. Return URL + required headers (Content-Type, x-amz-server-side-encryption) |
| `GeneratePresignedDownloadUrlAsync` | 1. `Verb.GET`<br>2. `ResponseHeaderOverrides` for `Content-Disposition: attachment` |
| `GeneratePresignedViewUrlAsync` | 1. `Verb.GET`<br>2. `ResponseHeaderOverrides` for `Content-Disposition: inline`, correct `Content-Type` |
| `BuildS3Key` | Return `tenants/{tenantId}/users/{userId}/{fileName}` (or `tenants/{tenantId}/{fileName}` for simplicity) |
| `BuildFolderS3Key` | Return `tenants/{tenantId}/{folderPath}/` |
| `ValidateS3KeyFormat` | Verify key starts with `tenants/{tenantId}/` |
| `ObjectExistsAsync` | `HeadObjectAsync` - catch 404 |
| `DeleteObjectAsync` | `DeleteObjectAsync` |

**AWS Config Required:**
- Bucket with CORS allowing PUT/GET from frontend origins
- IAM policy for presigned URL generation
- Server-side encryption (SSE-S3 or SSE-KMS)

---

### `TokenService` - `ITokenService`

**Dependencies:** `IConfiguration` (JWT settings), `ILogger`

| Method | Logic |
|--------|-------|
| `GenerateAccessToken` | 1. Create claims: `user_id`, `tenant_id`, `role`, `email`, `name`<br>2. Sign with HMAC-SHA256 (RS256 for production)<br>3. Expiry: 15-30 min |
| `GenerateRefreshToken` | Cryptographically random string (64 chars), store hash in DB with expiry (7-30 days) |
| `ValidateTokenAsync` | 1. Validate signature, expiry, issuer, audience<br>2. Return `TokenValidationResult` with parsed claims |
| `GetPrincipalFromExpiredToken` | Validate signature only (ignore expiry) for refresh flow |

**JWT Settings (appsettings.json):**
```json
{
  "Jwt": {
    "SecretKey": "your-256-bit-secret",
    "Issuer": "mini-drive",
    "Audience": "mini-drive-clients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

---

## Missing Pieces / Additional Components Needed

### 1. **Service Registration** (`Business/ServiceRegistration.cs`)
```csharp
public static class BusinessRegistration
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IS3Service, S3Service>();
        services.AddScoped<ITokenService, TokenService>();

        // AWS SDK
        services.AddAWSService<IAmazonS3>();
        services.Configure<S3Options>(config.GetSection("AWS:S3"));

        // Password hashing
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}
```

### 2. **Password Hasher Interface** (Extract from AuthService for testability)
```csharp
// Business/Interfaces/IPasswordHasher.cs
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
```

### 3. **Global Exception Middleware** (See `03-Exceptions.md`)
Register in `Program.cs`:
```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();
```

### 4. **Authentication/Authorization Setup** (Program.cs)
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!))
        };
    });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("TenantAdmin", p => p.RequireRole("TenantAdmin", "SuperAdmin"));
    options.AddPolicy("User", p => p.RequireRole("User", "TenantAdmin", "SuperAdmin"));
});
```

### 5. **Tenant Resolution Middleware** (Extract `tenantId` from JWT claims)
```csharp
public class TenantResolutionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantIdClaim = context.User.FindFirst("tenant_id");
        if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
        {
            context.Items["TenantId"] = tenantId;
        }
        await next(context);
    }
}
```

### 6. **Configuration Classes**
```csharp
// Business/Configuration/S3Options.cs
public class S3Options
{
    public string BucketName { get; set; } = "mini-drive";
    public string Region { get; set; } = "us-east-1";
    public string? Endpoint { get; set; } // For LocalStack/minio
}

// Business/Configuration/JwtOptions.cs
public class JwtOptions
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "mini-drive";
    public string Audience { get; set; } = "mini-drive-clients";
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
```

### 7. **Entity ↔ DTO Mapping Extensions** (or AutoMapper profile)
```csharp
// Business/Mappings/MappingExtensions.cs
public static class MappingExtensions
{
    public static FileResponse ToDto(this FileEntity e) => new(
        e.Id, e.TenantId, e.OwnerId, e.FolderId, e.OriginalName,
        e.MimeType, e.SizeBytes, e.S3Key, e.IsDeleted, e.DeletedAt,
        e.CreatedAt, e.UpdatedAt);

    public static TenantResponse ToDto(this Tenant t) => new(
        t.Id, t.Name, t.Slug, t.IsPersonal, t.StorageQuotaBytes, t.CreatedAt);

    // ... etc for User, Folder
}
```

### 8. **Unit Test Projects** (Structure)
```
tests/
├── MiniDriveBackend.Business.Tests/
│   ├── AuthServiceTests.cs
│   ├── FileServiceTests.cs
│   ├── StorageServiceTests.cs
│   └── TokenServiceTests.cs
└── MiniDriveBackend.Api.Tests/
    └── Controllers/
```

---

## Implementation Priority Order

| Priority | Service | Reason |
|----------|---------|--------|
| 1 | `TokenService` | Foundation for auth |
| 2 | `AuthService` | Login/registration needed first |
| 3 | `TenantService` | Tenant creation + quota |
| 4 | `StorageService` | Quota validation for uploads |
| 5 | `S3Service` | Presigned URLs for upload/download |
| 6 | `FileService` | Core file operations (depends on above) |
| 7 | `FolderService` | Folder hierarchy |
| 8 | `UserService` | Admin user management |

---

## Configuration Required (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=mini_drive;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-at-least-32-chars",
    "Issuer": "mini-drive",
    "Audience": "mini-drive-clients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "AWS": {
    "S3": {
      "BucketName": "mini-drive-files",
      "Region": "us-east-1",
      "Endpoint": "http://localhost:9000"  // For LocalStack
    }
  },
  "Storage": {
    "DefaultQuotaBytes": 1073741824
  }
}
```

---

## NuGet Packages Needed

Add to `miniDriveBackend.csproj`:
```xml
<ItemGroup>
  <!-- Auth -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
  <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.6.0" />

  <!-- AWS -->
  <PackageReference Include="AWSSDK.S3" Version="4.0.0" />
  <PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="4.0.0" />

  <!-- Validation -->
  <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />

  <!-- Testing -->
  <PackageReference Include="Moq" Version="4.20.70" />
  <PackageReference Include="xunit" Version="2.9.0" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
</ItemGroup>
```