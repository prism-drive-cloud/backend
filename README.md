# miniDriveBackend - Complete Technical Documentation

## Table of Contents

1. [Project Requirements & Scope](#1-project-requirements--scope)
2. [Architecture Overview](#2-architecture-overview)
3. [Database Layer](#3-database-layer)
4. [Data Access Object (DAO) Layer](#4-data-access-object-dao-layer)
5. [Business Layer](#5-business-layer)
6. [Docker & Deployment](#6-docker--deployment)

---

## 1. Project Requirements & Scope

### 1.1 Core Concept: Multi-Tenant File Storage

**Mini Drive** is a multi-tenant file storage application with two modes:
- **Corporate Mode (Multi-Tenant)**: Organizations register a Tenant (company) with isolated data and 1 GB storage quota. Corporate Admins manage users.
- **Personal Mode**: Individual users with 1 GB personal storage (modeled as a tenant with `is_personal = true`).

### 1.2 User Roles & Hierarchy

| Role | Scope | Capabilities |
|------|-------|--------------|
| **Super Admin** | Global | Views all tenants, global S3 consumption, system metrics |
| **Tenant Admin** | Single Tenant | Manages users, reviews storage consumption, audits activity |
| **User** | Own Resources | Upload, download, organize, preview, share files |

### 1.3 Core User Flows

1. **Onboarding**: Corporate registration (tenant + admin) → Personal registration → Corporate user invitations
2. **Authentication**: Email/password → JWT with `user_id`, `tenant_id`, `role`
3. **File Upload**: Drag & drop (Web) / Camera/Gallery (Mobile) → Request presigned URL → Direct S3 upload → Confirm metadata
4. **File Access**: List → Preview (presigned view URL) → Download (presigned download URL)
5. **Sharing & Security** (Phase 2): Temporary links, Vault/Strong folder with PIN

### 1.4 MVP vs Phase 2 (Desirable)

**MVP (Critical - Days 1-4)**
- Strict multi-tenant isolation (DB + S3 paths `/tenants/{tenant_id}/...`)
- JWT auth with 3 roles
- File CRUD (create, list, rename, move, soft delete)
- Drag & Drop with progress bar
- Mobile login, list, upload
- Direct S3 via presigned URLs (no binary through API)
- 1 GB quota enforcement
- Secure download URLs
- Web/Mobile sync
- Docker + Staging deployment

**Phase 2 (Desirable)**
- Power BI Dashboard
- Multimedia player/PDF viewer
- Folder upload (recursive)
- Vault/Strong folder (secondary PIN)
- Temporary share links
- Recycle bin & restore
- Nested folders

### 1.5 MVP Endpoint Catalog

| Module | Endpoint | Purpose |
|--------|----------|---------|
| **Auth** | `POST /api/v1/auth/register-tenant` | Create company + admin |
| | `POST /api/v1/auth/register-user` | Create user (personal or corporate) |
| | `POST /api/v1/auth/login` | JWT issuance |
| | `GET /api/v1/auth/me` | Current user + tenant profile |
| **Quota** | `GET /api/v1/tenants/usage` | Used vs 1 GB limit |
| **Files** | `GET /api/v1/files` | Paginated, searchable list |
| | `POST /api/v1/files/upload-url` | Get presigned URL (validates quota) |
| | `POST /api/v1/files/confirm` | Register metadata post-S3 upload |
| | `GET /api/v1/files/{id}/download-url` | Presigned download/view URL |
| | `PATCH /api/v1/files/{id}` | Rename or move |
| | `DELETE /api/v1/files/{id}` | Soft delete |
| **Folders** | `POST /api/v1/folders` | Create folder |
| **Sharing (Phase 2)** | `POST /api/v1/files/{id}/share` | Create expiring share link |
| | `POST /api/v1/vault/verify` | Verify Vault PIN |
| | `GET /api/v1/vault/files` | List Vault files |
| **Analytics** | `GET /api/v1/analytics/overview` | Aggregated metrics |

### 1.6 5-Day Schedule

| Day | Focus |
|-----|-------|
| **Mon** | Leaders alignment, Swagger mocks, S3/IAM setup |
| **Tue** | Frontend mocks, Backend models/auth/isolation, DevOps Staging |
| **Wed** | S3 integration + quota, Real API connection, Power BI connect |
| **Thu** | E2E integration, QA multi-tenant leak tests, Bug fixes |
| **Fri** | Code freeze, Documentation, Live Demo |

---

## 2. Architecture Overview

```
┌─────────────────────────────────────┐
│         Controllers (API)           │
├─────────────────────────────────────┤
│        Business Layer               │
│  ┌──────────┬──────────┬─────────┐  │
│  │Interfaces│   DTOs   │Exceptions│  │
│  └──────────┴──────────┴─────────┘  │
├─────────────────────────────────────┤
│      Data Access Layer              │
│       (Repositories)                │
├─────────────────────────────────────┤
│         Database (PostgreSQL)       │
└─────────────────────────────────────┘
```

**Layer Responsibilities:**
- **Controllers**: HTTP concerns only (routing, serialization, status codes)
- **Business Layer**: Business logic, validation, orchestration, multi-tenancy enforcement
- **DAO Layer**: Pure data access, tenant-scoped queries, EF Core abstraction
- **Database**: Schema, constraints, triggers for defense-in-depth isolation

---

## 3. Database Layer

### 3.1 Schema Overview (PostgreSQL 15+)

Source: `scripts/schema.sql`

#### Tables

**tenants**
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| name | TEXT | NOT NULL |
| slug | TEXT | NOT NULL, UNIQUE |
| is_personal | BOOLEAN | NOT NULL, DEFAULT false |
| storage_quota_bytes | BIGINT | NOT NULL, DEFAULT 1073741824 (1 GB), CHECK > 0 |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| **Indexes**: `uq_tenants_slug` (unique on slug) |
| **Trigger**: `trg_tenants_updated_at` |

**users**
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | FK → tenants(id) ON DELETE CASCADE, NULL allowed |
| email | TEXT | NOT NULL, UNIQUE |
| password_hash | TEXT | NOT NULL |
| full_name | TEXT | NOT NULL |
| role | TEXT | NOT NULL, CHECK IN ('super_admin','tenant_admin','user') |
| is_active | BOOLEAN | NOT NULL, DEFAULT true |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| **Constraints**: `uq_users_email`, `chk_users_role`, `chk_users_super_admin_no_tenant` |
| **Indexes**: `idx_users_tenant_id` |
| **Trigger**: `trg_users_updated_at` |

**folders** (MVP: flat, no nesting)
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | NOT NULL, FK → tenants(id) ON DELETE CASCADE |
| owner_id | UUID | NOT NULL, FK → users(id) ON DELETE RESTRICT |
| name | TEXT | NOT NULL |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| **Indexes**: `idx_folders_tenant_id`, `idx_folders_owner_id` |
| **Triggers**: `trg_folders_updated_at`, `trg_folders_validate_tenant` |

**files** (metadata only, binary in S3)
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | NOT NULL, FK → tenants(id) ON DELETE CASCADE |
| owner_id | UUID | NOT NULL, FK → users(id) ON DELETE RESTRICT |
| folder_id | UUID | NULL, FK → folders(id) ON DELETE SET NULL |
| original_name | TEXT | NOT NULL |
| mime_type | TEXT | NOT NULL |
| size_bytes | BIGINT | NOT NULL, CHECK >= 0 |
| s3_key | TEXT | NOT NULL, UNIQUE |
| is_deleted | BOOLEAN | NOT NULL, DEFAULT false |
| deleted_at | TIMESTAMPTZ | NULL |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| **Constraints**: `uq_files_s3_key`, `chk_files_size_positive`, `chk_files_deleted_at` |
| **Indexes**: `idx_files_tenant_id`, `idx_files_owner_id`, `idx_files_folder_id`, `idx_files_tenant_active` (partial WHERE is_deleted=false) |
| **Triggers**: `trg_files_updated_at`, `trg_files_validate_tenant` |

### 3.2 Multi-Tenant Isolation Triggers (Defense in Depth)

1. **folders**: `fn_validate_folder_tenant_consistency()` — ensures `folder.owner_id` belongs to `folder.tenant_id`
2. **files**: `fn_validate_file_tenant_consistency()` — ensures `file.owner_id` AND `file.folder_id` (if set) belong to `file.tenant_id`

These run BEFORE INSERT/UPDATE and raise exceptions on violations.

### 3.3 Entity Models

All entities inherit from `BaseEntity`:

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Tenant** (`Models/Tenant.cs`)
```csharp
public class Tenant : BaseEntity
{
    [Required, MaxLength(255)] public string Name { get; set; }
    [Required, MaxLength(100)] public string Slug { get; set; }
    public bool IsPersonal { get; set; } = false;
    public long StorageQuotaBytes { get; set; } = 1073741824; // 1 GB
}
```

**UserRole** (`Models/UserRole.cs`)
```csharp
public enum UserRole { SuperAdmin, TenantAdmin, User }
```

**User** (`Models/User.cs`)
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

**Folder** (`Models/Folder.cs`) — flat structure, no parent_id
```csharp
public class Folder : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OwnerId { get; set; }
    [Required, MaxLength(255)] public string Name { get; set; }
}
```

**FileEntity** (`Models/FileEntity.cs`) — renamed to avoid System.IO.File conflict
```csharp
public class FileEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? FolderId { get; set; }
    [Required, MaxLength(255)] public string OriginalName { get; set; }
    [Required, MaxLength(100)] public string MimeType { get; set; }
    public long SizeBytes { get; set; }
    [Required, MaxLength(500)] public string S3Key { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
```

### 3.4 DbContext Configuration (`Data/AppDbContext.cs`)

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<FileEntity> Files { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBaseEntity(modelBuilder);    // Id, CreatedAt, UpdatedAt defaults
        ConfigureTenant(modelBuilder);        // Table: tenants, unique slug
        ConfigureUser(modelBuilder);          // Table: users, role→string, nullable TenantId
        ConfigureFolder(modelBuilder);        // Table: folders, FKs CASCADE/RESTRICT
        ConfigureFile(modelBuilder);          // Table: files, soft delete global filter
    }
}
```

**Key Configuration Decisions:**
| Decision | Implementation |
|----------|----------------|
| No navigation properties | FKs only, per requirement |
| Fluent API over Data Annotations | Centralized in OnModelCreating |
| BaseEntity reflection | DRY for common columns |
| Soft delete via global filter | `FileEntity.IsDeleted` filtered automatically |
| Enum → string conversion | `UserRole` stored as TEXT |
| snake_case column mapping | Matches PostgreSQL convention |
| DB triggers for UpdatedAt | `trigger_set_updated_at()` function |
| Multi-tenant isolation | DB triggers + app-layer validation |

### 3.5 Architecture Decisions (from `decisions.md`)

1. **Personal Accounts as Tenants** — `is_personal` flag, unified isolation logic
2. **Flat Folders for MVP** — No `parent_folder_id`; additive migration path exists
3. **Nullable TenantId only for SuperAdmin** — CHECK constraint enforces; avoids fictitious tenant or separate table
4. **Live Quota Calculation** — `SUM(size_bytes)` at query time; no cached counter; partial index optimizes
5. **Cross-Validation Triggers** — DB-level tenant consistency for folders/files
6. **Direct Corporate User Creation** — No invitations table; admin creates directly

### 3.6 Backend Credentials Required

1. `DATABASE_URL` (Supabase Connection Pooling URL preferred)
2. AWS S3 credentials (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `AWS_S3_BUCKET`)
3. `JWT_SECRET` for token signing

---

## 4. Data Access Object (DAO) Layer

### 4.1 Structure

```
Data/
├── Interfaces/           # Repository contracts
│   ├── ITenantRepository.cs
│   ├── IUserRepository.cs
│   ├── IFolderRepository.cs
│   └── IFileRepository.cs
├── Repositories/         # Concrete implementations
│   ├── BaseRepository.cs     # Shared CRUD
│   ├── TenantRepository.cs
│   ├── UserRepository.cs
│   ├── FolderRepository.cs
│   └── FileRepository.cs
└── DAORegistration.cs    # DI extension method
```

### 4.2 Design Principles

1. **Interface Segregation** — Each entity has specialized interface (no generic `IRepository<T>`)
2. **Multi-Tenant Isolation** — All queries filter by `tenantId` (except SuperAdmin)
3. **Explicit Contracts** — Method signatures match MVP endpoints
4. **No Navigation Properties** — Entities use FKs; repositories handle joins explicitly
5. **Service Layer Owns Transactions** — Repositories are stateless; `DbContext` is Scoped

### 4.3 BaseRepository<T>

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

Concrete repositories override `GetByIdAsync` to add tenant filter (Folder, File) or keep global (Tenant, User).

### 4.4 Repository Interfaces & Implementations

#### ITenantRepository / TenantRepository
| Method | Purpose | Endpoint |
|--------|---------|----------|
| `GetByIdAsync(id)` | Retrieve tenant | `GET /auth/me` |
| `GetBySlugAsync(slug)` | Slug lookup | `POST /register-tenant` |
| `CreateAsync(tenant)` | Insert tenant | `POST /register-tenant` |
| `GetUsageAsync(tenantId)` | Live SUM of file sizes | `GET /tenants/usage` |
| `ExistsBySlugAsync(slug)` | Uniqueness check | `POST /register-tenant` |

**Key**: No tenant filter on tenant queries (root of isolation). `GetUsageAsync` uses `Context.Files` directly with partial index `idx_files_tenant_active`.

#### IUserRepository / UserRepository
| Method | Purpose | Endpoint |
|--------|---------|----------|
| `GetByIdAsync(id)` | Global lookup (login, token refresh) | `POST /login` |
| `GetByEmailAsync(email)` | Login lookup | `POST /login` |
| `GetByIdAndTenantAsync(id, tenantId)` | Tenant-scoped lookup | File/folder ops |
| `GetByTenantIdAsync(tenantId)` | List tenant users | Admin UI |
| `GetSuperAdminsAsync()` | Global SuperAdmins | Platform admin |
| `CreateAsync(user)` | Insert user | Registration |
| `UpdateAsync(user)` | Update profile/role | Admin actions |
| `ExistsByEmailAsync(email)` | Uniqueness check | Registration |
| `ExistsByIdAndTenantAsync(id, tenantId)` | Ownership validation | Before operations |

**Three Lookup Patterns:**
1. Global (no tenant): `GetByIdAsync`, `GetByEmailAsync`
2. Tenant-scoped: `GetByIdAndTenantAsync`, `GetByTenantIdAsync`, `ExistsByIdAndTenantAsync`
3. SuperAdmin: `GetSuperAdminsAsync()` (role + NULL tenant_id)

#### IFolderRepository / FolderRepository
| Method | Purpose | Endpoint |
|--------|---------|----------|
| `GetByIdAsync(id, tenantId)` | Single folder | `GET /files/{id}`, move validation |
| `GetByTenantIdAsync(tenantId)` | All folders (flat) | `GET /files` |
| `GetRootFoldersAsync(tenantId)` | Alias for above (MVP) | `GET /files?folder_id=null` |
| `CreateAsync(folder)` | Create folder | `POST /folders` |
| `UpdateAsync(folder)` | Rename folder | `PATCH /files/{id}` |
| `DeleteAsync(id, tenantId)` | Hard delete | `DELETE /files/{id}` |
| `ExistsByIdAndTenantAsync(id, tenantId)` | Validation | File operations |

**MVP Decision**: Flat structure — `GetRootFoldersAsync == GetByTenantIdAsync`. Future: add `parent_folder_id` + `GetChildrenAsync`.

#### IFileRepository / FileRepository
| Method | Purpose | Endpoint |
|--------|---------|----------|
| `GetByIdAsync(id, tenantId)` | Single file | `GET /files/{id}`, download, rename |
| `GetByTenantIdAsync(tenantId, page, pageSize, search)` | Paginated list | `GET /files` |
| `GetByFolderIdAsync(folderId, tenantId)` | Files in folder | `GET /files?folder_id={id}` |
| `GetRootFilesAsync(tenantId)` | Files at root | `GET /files?folder_id=null` |
| `GetTotalCountByTenantIdAsync(tenantId, search)` | Pagination count | `GET /files` |
| `GetTotalSizeByTenantIdAsync(tenantId)` | Quota SUM | `GET /tenants/usage`, pre-upload |
| `CreateAsync(file)` | Register metadata | `POST /files/confirm` |
| `UpdateAsync(file)` | Rename/move | `PATCH /files/{id}` |
| `SoftDeleteAsync(id, tenantId)` | Logical delete | `DELETE /files/{id}` |
| `ExistsByIdAndTenantAsync(id, tenantId)` | Access validation | All file ops |
| `ExistsByS3KeyAsync(s3Key)` | Idempotency check | `POST /files/confirm` |

**Key Features:**
- Global query filter handles soft delete automatically
- Live quota via `SUM()` with partial index optimization
- S3 key uniqueness at DB + app level
- Simple `Contains` search on `OriginalName`

### 4.5 DI Registration (`Data/DAORegistration.cs`)

```csharp
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
```

**Lifetime**: All `AddScoped` — shares `AppDbContext` per HTTP request.

**Usage in Program.cs:**
```csharp
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDataAccess();
```

---

## 5. Business Layer

### 5.1 Service Interfaces (7 interfaces)

| Interface | Responsibility | Key Methods |
|-----------|----------------|-------------|
| `ITokenService` | JWT generation/validation | `GenerateAccessToken`, `GenerateRefreshToken`, `ValidateTokenAsync` |
| `IAuthService` | Authentication flows | `LoginAsync`, `RegisterTenantAsync`, `RegisterUserAsync`, `GetCurrentUserAsync` |
| `ITenantService` | Tenant lifecycle & quotas | `CreateTenantAsync`, `GetUsageAsync`, `ValidateQuotaAsync`, `ExistsBySlugAsync` |
| `IFileService` | File CRUD + S3 orchestration | `GetFilesAsync`, `RequestUploadUrlAsync`, `ConfirmUploadAsync`, `GetDownloadUrlAsync`, `RenameAsync`, `MoveAsync`, `SoftDeleteAsync` |
| `IFolderService` | Folder hierarchy | `CreateFolderAsync`, `GetFoldersAsync`, `GetFolderByIdAsync`, `RenameFolderAsync`, `DeleteFolderAsync` |
| `IUserService` | User management | `CreateUserAsync`, `GetUsersByTenantAsync`, `UpdateUserAsync`, `Activate/Deactivate` |
| `IStorageService` | Quota validation | `CheckQuotaAvailableAsync`, `GetStorageInfoAsync`, `Reserve/ReleaseQuotaAsync` |
| `IS3Service` | AWS S3 abstraction | `GeneratePresignedUploadUrlAsync`, `GeneratePresignedDownloadUrlAsync`, `BuildS3Key`, `ObjectExistsAsync` |

### 5.2 DTOs (Data Transfer Objects)

**Why DTOs over Entities:**
- Security: Prevent over-posting (hide PasswordHash, internal IDs)
- API Stability: Entity changes don't break contracts
- Serialization: No circular refs, explicit camelCase control
- Performance: Tailored shapes (ListItem vs Detail)

**DTO Categories:**
- **Request DTOs** — Input with validation attributes
- **Response DTOs** — Output with computed/aggregated data
- **Query/Parameter DTOs** — Pagination, filtering, sorting
- **Wrapper DTOs** — `PagedResult<T>` for consistent pagination

**DTO Catalog:**

| File | DTOs |
|------|------|
| `AuthDtos.cs` | `LoginRequest`, `RegisterTenantRequest`, `RegisterUserRequest`, `AuthResponse`, `UserProfileResponse`, `TokenRefreshRequest`, `ChangePasswordRequest` |
| `TenantDtos.cs` | `CreateTenantRequest`, `TenantResponse`, `TenantUsageResponse` |
| `FileDtos.cs` | `FileQueryParameters`, `FileResponse`, `UploadUrlRequest/Response`, `ConfirmUploadRequest`, `DownloadUrlResponse`, `RenameFileRequest`, `MoveFileRequest`, `PagedResult<T>` |
| `FolderDtos.cs` | `CreateFolderRequest`, `FolderResponse`, `RenameFolderRequest`, `FolderTreeResponse` |
| `UserDtos.cs` | `CreateUserRequest`, `UserResponse`, `UpdateUserRequest` |

**Pattern**: Use `record` types for immutability, value equality, with-expressions.

### 5.3 Custom Exceptions

**Base**: `BusinessException` with `ErrorCode` (machine-readable) and `StatusCode` (HTTP mapping).

| Exception | HTTP | Error Code | Scenario |
|-----------|------|------------|----------|
| `QuotaExceededException` | 400 | `QUOTA_EXCEEDED` | Upload exceeds 1 GB |
| `TenantNotFoundException` | 404 | `TENANT_NOT_FOUND` | Invalid tenant |
| `UserNotFoundException` | 404 | `USER_NOT_FOUND` | User not found |
| `FileNotFoundException` | 404 | `FILE_NOT_FOUND` | File not found/cross-tenant |
| `FolderNotFoundException` | 404 | `FOLDER_NOT_FOUND` | Folder not found |
| `UnauthorizedAccessException` | 403 | `UNAUTHORIZED_ACCESS` | Cross-tenant attempt |
| `InvalidCredentialsException` | 401 | `INVALID_CREDENTIALS` | Wrong password |
| `DuplicateResourceException` | 409 | `DUPLICATE_RESOURCE` | Unique constraint violation |
| `S3OperationException` | 500 | `S3_OPERATION_FAILED` | AWS SDK errors |

**Global Exception Middleware** maps to RFC 7807 ProblemDetails JSON.

### 5.4 Service Implementation Requirements

All services follow:
- Constructor injection (repositories, services, logger)
- `CancellationToken` propagation
- Structured logging (Info/Warning/Error)
- Input validation before repository calls
- Tenant isolation enforcement on every call

#### AuthService (`IAuthService`)
**Dependencies**: `IUserRepository`, `ITenantRepository`, `ITokenService`, `IPasswordHasher`
- `LoginAsync`: Email lookup → BCrypt verify → IsActive check → JWT → `AuthResponse`
- `RegisterTenantAsync`: Slug uniqueness → Create Tenant + TenantAdmin User → Hash password → Transaction → JWT
- `RegisterUserAsync`: Validate caller role → Email uniqueness → Create User → Hash password
- Password hashing: BCrypt.Net-Next or Argon2

#### TenantService (`ITenantService`)
**Dependencies**: `ITenantRepository`, `IFileRepository`, `IFolderRepository`
- `GetUsageAsync`: Aggregates file size (SUM), file count, folder count → `TenantUsageResponse` with computed percentage

#### FileService (`IFileService`) — Most Complex
**Dependencies**: `IFileRepository`, `IFolderRepository`, `ITenantRepository`, `IStorageService`, `IS3Service`
- **Two-Phase Upload Flow**:
  1. `RequestUploadUrlAsync`: Check quota → Build S3 key (`tenants/{tenantId}/users/{userId}/{fileName}`) → Check S3 key uniqueness → Generate presigned PUT URL (15 min) → Return URL + headers
  2. Client uploads binary directly to S3
  3. `ConfirmUploadAsync`: Verify S3 object exists → Create FileEntity metadata → Save → Release quota reservation
- `GetDownloadUrlAsync`: Validate access → Generate presigned GET URL (attachment or inline for preview)

#### StorageService (`IStorageService`)
**Dependencies**: `ITenantRepository`, `IFileRepository`
- `CheckQuotaAvailableAsync`: `used + requested <= quota` (live SUM)
- MVP: Check at RequestUploadUrl + ConfirmUpload; Production: add reservation/locking

#### S3Service (`IS3Service`)
**Dependencies**: `IAmazonS3`, `IConfiguration` (bucket, region)
- `BuildS3Key`: `tenants/{tenantId}/users/{userId}/{fileName}`
- Presigned URLs: PUT for upload, GET for download (attachment), GET for view (inline + Content-Type)
- CORS, IAM, SSE-S3/KMS handled in AWS config

#### TokenService (`ITokenService`)
**Dependencies**: `IConfiguration` (JWT settings)
- Access token: 15-30 min, claims: `user_id`, `tenant_id`, `role`, `email`, `name`
- Refresh token: Cryptographically random, stored hashed with 7-30 day expiry
- RS256 for production, HS256 for dev

### 5.5 Missing Components to Implement

1. **ServiceRegistration.cs** — `AddBusinessServices()` extension
2. **IPasswordHasher** interface + BCrypt implementation
3. **GlobalExceptionMiddleware** — Maps exceptions to ProblemDetails
4. **JWT Authentication Setup** — `AddAuthentication().AddJwtBearer()`
5. **Authorization Policies** — SuperAdmin, TenantAdmin, User roles
6. **TenantResolutionMiddleware** — Extract `tenant_id` from JWT claims to `HttpContext.Items`
7. **Configuration Classes** — `S3Options`, `JwtOptions`, `StorageOptions`
8. **Entity ↔ DTO Mapping Extensions** — Manual or AutoMapper
9. **Unit Test Projects** — Business.Tests + Api.Tests

### 5.6 Required NuGet Packages

```xml
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
```

### 5.7 Required Configuration (appsettings.json)

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
      "Endpoint": "http://localhost:9000"
    }
  },
  "Storage": {
    "DefaultQuotaBytes": 1073741824
  }
}
```

---

## 6. Docker & Deployment

### 6.1 Dockerfile (`.NET Application`)

```dockerfile
# Base image: mcr.microsoft.com/dotnet/aspnet:10.0 (runtime)
# Build stage: mcr.microsoft.com/dotnet/sdk:10.0
# Working directory: /app
# Exposed port: 8080
# Entry point: dotnet miniDriveBackend.dll
```

### 6.2 docker-compose.yml

```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: minidrive
      POSTGRES_USER: minidrive_user
      POSTGRES_PASSWORD: minidrive_pass
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/schema.sql:/docker-entrypoint-initdb.d/01-schema.sql
      - ./scripts/seed_data.sql:/docker-entrypoint-initdb.d/02-seed_data.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U minidrive_user -d minidrive"]
      interval: 5s
      timeout: 5s
      retries: 5
    ports:
      - "5432:5432"

  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=db;Database=minidrive;Username=minidrive_user;Password=minidrive_pass"
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

volumes:
  postgres_data:
```

### 6.3 Key Design Decisions

| Aspect | Decision |
|--------|----------|
| DB initialization | PostgreSQL runs `schema.sql` + `seed_data.sql` |
| Healthcheck | `pg_isready` verifies PostgreSQL readiness |
| Port | API on 8080; PostgreSQL on 5432 |
| Network | Docker Compose internal network |
| Volumes | `postgres_data` persists PostgreSQL data |
| Single command | `docker-compose up --build` |

### 6.4 .dockerignore

```
bin/
obj/
.git/
*.md
docker-compose.yml
Dockerfile
```

### 6.5 AppSettings for Docker

Add connection string to:
- `appsettings.json`
- `appsettings.Development.json`

Use environment variable substitution: `ConnectionStrings__DefaultConnection`

### 6.6 Execution

```bash
docker-compose up --build
```

This will:
1. Build the .NET application image
2. Start PostgreSQL with schema and seed data
3. Wait for database healthcheck
4. Start API on port 8080
5. Run complete stack with single command

---

## Appendix: Key Cross-Cutting Concerns

### Multi-Tenancy Enforcement
- **Database**: Triggers validate owner/folder belong to same tenant
- **DAO**: All repository methods require explicit `tenantId` (except SuperAdmin)
- **Business**: Services validate tenant on every operation
- **API**: TenantResolutionMiddleware extracts `tenant_id` from JWT claims

### Quota Management
- Live calculation: `SUM(size_bytes) WHERE tenant_id = X AND is_deleted = false`
- Partial index `idx_files_tenant_active` optimizes
- Checked at `RequestUploadUrlAsync` (pre-upload) and `ConfirmUploadAsync` (post-upload)
- No cached counter — avoids desync bugs

### Soft Delete Pattern
- Only `FileEntity` implements soft delete
- Global query filter: `HasQueryFilter(e => !e.IsDeleted)`
- `SoftDeleteAsync` sets `IsDeleted = true`, `DeletedAt = now()`
- To include deleted: `Context.Files.IgnoreQueryFilters()`

### S3 Key Structure
```
tenants/{tenantId}/users/{userId}/{fileName}
```
Enforced by `IS3Service.BuildS3Key()` and validated by `ValidateS3KeyFormat()`.

### Future Extensibility Points
| Feature | Change Required |
|---------|-----------------|
| Nested folders | Add `parent_folder_id`, `GetChildrenAsync` |
| File sharing | New `IShareRepository`, `Share` entity |
| Vault/Strong folder | New `IVaultRepository`, `Vault` entity |
| Recycle bin | Add `GetDeletedFilesAsync`, `RestoreAsync` |
| Audit logging | Decorator pattern on repositories |