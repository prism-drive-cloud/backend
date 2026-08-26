# Backend

`miniDriveBackend` is the backend for a multi-tenant "mini drive" file-storage
application. This branch implements the **authentication and multi-tenant
security infrastructure and its HTTP API**.

**Technology stack (as present in the repository):**

- **.NET 10** / ASP.NET Core (MVC controllers + minimal-API demo endpoint)
- **PostgreSQL** via **`Npgsql.EntityFrameworkCore.PostgreSQL`** (EF Core)
- **`Microsoft.AspNetCore.Authentication.JwtBearer`** for JWT authentication
- **`BCrypt.Net-Next`** for password hashing
- **`Microsoft.AspNetCore.OpenApi`** for the OpenAPI document (no Swagger UI package)

---

## Architecture

Request flow:

```
HTTP API
   ↓
Controllers            (Controllers/AuthController.cs)
   ↓
Business Services      (Business/Services/*)
   ↓
Repositories           (Data/Repositories/*)
   ↓
PostgreSQL             (scripts/schema.sql)
```

Layers that actually exist:

| Layer | Location | Responsibility |
|-------|----------|----------------|
| **API / host** | `Program.cs` | DI wiring, JWT authentication, authorization policies, OpenAPI, exception middleware, request pipeline. Keeps a demo `GET /weatherforecast` endpoint. |
| **Controllers** | `Controllers/` | `AuthController` — the authentication HTTP API. Thin: binds DTOs, reads the authenticated identity from `ITenantContext`, calls business services. |
| **Middleware** | `Middleware/` | `GlobalExceptionMiddleware` — maps business/validation exceptions to consistent HTTP responses. |
| **OpenAPI** | `OpenApi/` | Transformers that add the Bearer security scheme and per-operation security requirements to the OpenAPI document. |
| **Business** | `Business/` | Service interfaces, DTOs, exceptions, the implemented services (`AuthService`, `TokenService`, `TenantService`), security helpers, configuration, DI registration. |
| **Models** | `Models/` | EF Core entities: `Tenant`, `User`, `Folder`, `FileEntity`, `RefreshToken`, plus `BaseEntity` and the `UserRole` enum. |
| **Data / Repositories** | `Data/` | `AppDbContext` (EF Core configuration) and the repository pattern (`Data/Repositories/` + `Data/Interfaces/`). |
| **PostgreSQL** | `scripts/schema.sql` | SQL-first schema (source of truth), including multi-tenant consistency triggers. |

### Not implemented (contracts only / absent)

- `IUserService`, `IFileService`, `IFolderService`, `IStorageService`, `IS3Service`
  exist as **interfaces only** — there are no implementations.
- There is **no S3/object-storage integration** and no AWS SDK dependency. The
  `IS3Service` / `IStorageService` contracts describe intended shapes only.
- Only `AuthController` exists; there are no file/folder/user/tenant controllers.

---

## Authentication

Authentication code lives under `Business/Services/`, `Business/Security/`,
`Business/Interfaces/`, `Business/Configuration/`, and `Data/`.

| Component | File | Responsibility |
|-----------|------|----------------|
| **AuthService** | `Business/Services/AuthService.cs` | Login, tenant registration, user registration, token refresh, current-user lookup, credential validation, password change, refresh-token revocation. Enforces role/tenant rules. |
| **TokenService** | `Business/Services/TokenService.cs` | Generates/validates signed JWT access tokens; generates cryptographically random refresh tokens. |
| **TenantService** | `Business/Services/TenantService.cs` | Tenant creation/lookup (by id/slug), slug existence checks, usage summary, and quota validation. |
| **IPasswordHasher** | `Business/Interfaces/IPasswordHasher.cs` | Abstraction over password hashing (`Hash`, `Verify`). |
| **BCryptPasswordHasher** | `Business/Security/BCryptPasswordHasher.cs` | `IPasswordHasher` implementation using `BCrypt.Net-Next`. |
| **ITenantContext** | `Business/Interfaces/ITenantContext.cs` | Exposes the authenticated identity: `UserId`, `TenantId`, `Role`, `IsAuthenticated`. |
| **TenantContext** | `Business/Security/TenantContext.cs` | Reads `sub` / `tenant_id` / `role` claims from `IHttpContextAccessor.HttpContext.User`. Request-scoped. |
| **RefreshTokenRepository** | `Data/Repositories/RefreshTokenRepository.cs` | Persists refresh tokens, looks them up by hash, updates them, and revokes a user's active tokens. |

---

## Authentication API

All routes are under `api/auth` (`Controllers/AuthController.cs`). Request/response
DTOs are defined in `Business/DTOs/AuthDtos.cs` and `Business/DTOs/TenantDtos.cs`.

### Public endpoints

| Method | Route | Auth | Request DTO | Response DTO | Success | Purpose |
|--------|-------|------|-------------|--------------|---------|---------|
| POST | `/api/auth/login` | none | `LoginRequest` | `AuthResponse` | 200 | Authenticate with email + password. |
| POST | `/api/auth/register-tenant` | none | `RegisterTenantRequest` | `AuthResponse` | 201 | Create a tenant and its initial `TenantAdmin`. |
| POST | `/api/auth/refresh` | none | `TokenRefreshRequest` | `AuthResponse` | 200 | Exchange a refresh token for a new token pair (rotation). |

### Authenticated endpoints

| Method | Route | Auth | Request DTO | Response DTO | Success | Purpose |
|--------|-------|------|-------------|--------------|---------|---------|
| GET | `/api/auth/me` | `[Authorize]` | — | `UserProfileResponse` | 200 | Return the current user's profile. |
| POST | `/api/auth/logout` | `[Authorize]` | — | — | 204 | Revoke the current user's active refresh tokens. |
| POST | `/api/auth/change-password` | `[Authorize]` | `ChangePasswordRequest` | — | 200 | Change the current user's password (revokes active sessions). |

### TenantAdmin / SuperAdmin endpoint

| Method | Route | Policy | Request DTO | Response DTO | Success | Purpose |
|--------|-------|--------|-------------|--------------|---------|---------|
| POST | `/api/auth/register-user` | `TenantAdmin` (SuperAdmin or TenantAdmin) | `RegisterUserRequest` | `AuthResponse` | 201 | Create a user within the caller's tenant (see [Multi-Tenancy](#multi-tenancy)). |

The authenticated user id for `me`, `logout`, `change-password` and
`register-user` is taken from `ITenantContext.UserId` (never from the request).

### DTO fields (as defined in code)

- `LoginRequest`: `Email`, `Password`
- `RegisterTenantRequest`: `TenantName`, `Slug`, `AdminEmail`, `AdminPassword`, `AdminFullName`
- `RegisterUserRequest`: `Email`, `Password`, `FullName`, `Role` (defaults to `User`)
- `TokenRefreshRequest`: `RefreshToken`
- `ChangePasswordRequest`: `CurrentPassword`, `NewPassword`
- `AuthResponse`: `AccessToken`, `RefreshToken`, `ExpiresAt`, `User` (`UserProfileResponse`), `Tenant` (`TenantResponse?` — null for SuperAdmin)
- `UserProfileResponse`: `Id`, `Email`, `FullName`, `Role`, `IsActive`, `CreatedAt`
- `TenantResponse`: `Id`, `Name`, `Slug`, `IsPersonal`, `StorageQuotaBytes`, `CreatedAt`

No password or password-hash field is present in any response DTO.

### Status codes

Success codes are as listed above (`200`, `201`, `204`). Error codes are produced
consistently:

- **400** — request DTO validation failures (`[ApiController]` model validation) and
  `ArgumentException` from services (mapped by `GlobalExceptionMiddleware`).
- **401** — missing/invalid JWT (JwtBearer), invalid credentials, or invalid/expired
  refresh token (`InvalidCredentialsException`).
- **403** — authenticated but insufficient role/policy, or a forbidden tenant/role
  operation (`UnauthorizedAccessException`).
- **404** — `TenantNotFoundException` / `UserNotFoundException`.
- **409** — `DuplicateResourceException` (duplicate tenant slug or user email).

Business errors are mapped from `BusinessException.StatusCode` by
`Middleware/GlobalExceptionMiddleware.cs` (response body: `errorCode`, `message`,
`details`).

---

## JWT Authentication

Access tokens are JSON Web Tokens signed with **HMAC-SHA256**
(`Business/Services/TokenService.cs`).

**Claims emitted:**

| Claim | Value |
|-------|-------|
| `sub` | User id (GUID) |
| `role` | `SuperAdmin` / `TenantAdmin` / `User` (enum name) |
| `jti` | Unique token id (GUID) |
| `tenant_id` | Tenant id — **only present when the user has a tenant** |
| `email`, `name` | User email and full name |
| `iss` / `aud` | Issuer / audience (from configuration) |
| `iat` / `nbf` / `exp` | Issued-at / not-before / expiry |

Behavior:

- **Access tokens expire after 15 minutes** (`Jwt:AccessTokenExpiryMinutes`, default `15`).
- JWTs are **signed**; the raw signing key is never emitted or logged.
- The **ASP.NET Core JwtBearer middleware** validates signature, issuer, audience and
  lifetime (`Program.cs`).
- **`MapInboundClaims = false`** — inbound claims keep their raw names.
- **`RoleClaimType = "role"`** and **`NameClaimType = "sub"`** — consistent between the
  middleware and `TokenService`.
- **SuperAdmin may have `TenantId = null`** — the `tenant_id` claim is omitted entirely.

The signing key is **not** included in this document; see [Configuration](#configuration).

---

## Refresh Tokens

Implemented in `TokenService`, `AuthService`, `RefreshTokenRepository`, and the
`RefreshToken` entity.

- Refresh tokens are **256-bit cryptographically random** values (base64url encoded).
- **Only the SHA-256 hash is stored** in PostgreSQL; the raw token is returned to the
  client once and never persisted.
- **Expiration** is set at creation (`Jwt:RefreshTokenExpiryDays`, default `7`).
- **Rotation** — each refresh issues a new token and revokes the presented one.
- **`RevokedAt`** marks a token as no longer usable.
- **`ReplacedByTokenId`** links a rotated token to its successor.
- **Reuse detection** — presenting an already-revoked token revokes the user's entire
  active token chain and rejects the request.
- **Revocation** — `logout` revokes the user's active refresh tokens.
- **Password changes revoke active sessions.**

### `refresh_tokens` table

Defined in `scripts/schema.sql`; the EF Core mapping mirrors it in
`Data/AppDbContext.cs` (`ConfigureRefreshToken`):

| Column | Notes |
|--------|-------|
| `id` | UUID PK, `DEFAULT gen_random_uuid()` |
| `user_id` | UUID, `NOT NULL`, FK → `users(id)` **`ON DELETE CASCADE`** |
| `token_hash` | TEXT, `NOT NULL`, **UNIQUE** (`uq_refresh_tokens_token_hash`) |
| `expires_at` | TIMESTAMPTZ, `NOT NULL` |
| `revoked_at` | TIMESTAMPTZ, nullable |
| `replaced_by_token_id` | UUID, nullable, FK → `refresh_tokens(id)` `ON DELETE SET NULL` |
| `created_at` / `updated_at` | TIMESTAMPTZ, `DEFAULT now()`; `updated_at` maintained by trigger `trg_refresh_tokens_updated_at` |

Indexes: `uq_refresh_tokens_token_hash` (unique), `idx_refresh_tokens_user_id`.

---

## Password Security

- Hashing uses **`BCrypt.Net-Next`** via `IPasswordHasher` / `BCryptPasswordHasher`.
- Passwords are **hashed** on tenant/user registration and password change, and
  **verified** on login, credential validation, and password change.
- **Password hashes are never returned in API responses** (no response DTO contains
  a password/hash field) and are not logged.
- ⚠️ **`scripts/seed_data.sql` contains placeholder BCrypt hashes.** They are not real
  credentials — replace them with hashes produced by the application's hasher before
  attempting to log in as a seeded user.

---

## Multi-Tenancy

**The tenant is NEVER taken from a client-provided `tenant_id`.**

```
JWT
 ↓
ClaimsPrincipal        (populated by JwtBearer middleware)
 ↓
ITenantContext         (TenantContext — the only trusted source)
 ↓
TenantId / UserId / Role
 ↓
Business Service
 ↓
Tenant-aware Repository (WHERE ... AND tenant_id = @tenantId)
 ↓
PostgreSQL             (+ tenant-consistency triggers)
```

Rules enforced / relied upon:

- **Normal users belong to exactly one tenant** (`User.TenantId`).
- **SuperAdmin may have `TenantId = null`** and operates globally.
- **TenantAdmin is restricted to its own tenant** — `RegisterUserAsync` always uses
  `currentUser.TenantId`.
- **TenantAdmin cannot create a SuperAdmin.**
- **TenantAdmin cannot choose another tenant** — no request DTO carries a tenant id.
- **Tenant-scoped repositories must use the authenticated `TenantId`.** Repositories
  expose tenant-scoped methods (e.g. `GetByIdAndTenantAsync`,
  `GetByIdAsync(id, tenantId)`, `ExistsByIdAndTenantAsync`).
- **PostgreSQL tenant-consistency triggers** (`fn_validate_folder_tenant_consistency`,
  `fn_validate_file_tenant_consistency`) reject cross-tenant rows — defense in depth.

> ⚠️ **Future developers:** DO NOT create controllers that accept `tenantId` from the
> request body, query string, headers, or route parameters as the authoritative
> tenant. Always resolve the tenant from `ITenantContext`.

---

## Authorization

Authorization uses the existing ASP.NET Core JWT + policy configuration in
`Program.cs`. Policies (role names match the `role` claim values):

- **`SuperAdmin`** → requires role `SuperAdmin`
- **`TenantAdmin`** → requires role `SuperAdmin` or `TenantAdmin`
- **`User`** → requires role `SuperAdmin`, `TenantAdmin`, or `User`

Currently applied: `register-user` uses `[Authorize(Policy = "TenantAdmin")]`; `me`,
`logout` and `change-password` use `[Authorize]`. There is a **single** JWT
authentication scheme — do not add a second one.

---

## Database

The project is **SQL-first**.

- **`scripts/schema.sql` is the source of truth** for the schema (PostgreSQL 15+):
  tables, constraints, indexes, and multi-tenant consistency triggers.
- **`refresh_tokens` was added to `scripts/schema.sql`** with:
  - a **foreign key to `users`** with **`ON DELETE CASCADE`**,
  - **token hash uniqueness** (`uq_refresh_tokens_token_hash`),
  - a **user index** (`idx_refresh_tokens_user_id`),
  - an **`updated_at` trigger** (`trg_refresh_tokens_updated_at`),
  - a **`replaced_by_token_id`** self-relationship (FK → `refresh_tokens(id)`
    `ON DELETE SET NULL`) supporting rotation.
- The **EF Core entity/configuration mirrors the SQL schema**
  (`Models/RefreshToken.cs`, `Data/AppDbContext.cs`).
- **EF Core migrations are NOT used** — there is no `Migrations/` folder and no
  `Microsoft.EntityFrameworkCore.Design` dependency.

---

## OpenAPI

The API exposes an OpenAPI document at **`/openapi/v1.json`** (Development
environment), via `Microsoft.AspNetCore.OpenApi`.

A **Bearer JWT security scheme** is declared (`OpenApi/BearerSecurityTransformers.cs`):

- `type: http`
- `scheme: bearer`
- `bearerFormat: JWT`

Authenticated endpoints declare the Bearer security requirement; public endpoints
(`login`, `register-tenant`, `refresh`) do not.

> There is **no Swagger UI** package in this repository — only the OpenAPI document is
> served. Any OpenAPI-compatible viewer that consumes `/openapi/v1.json` will render
> the "Authorize" affordance because the document declares the Bearer scheme.

---

## Dependency Injection

Registration is split across two extension methods, both called from `Program.cs`
(`AddDbContext<AppDbContext>` with Npgsql is also registered there, and `AddControllers()`):

- **`AddDataAccess()`** (`Data/DAORegistration.cs`) — repositories (scoped):
  `ITenantRepository`, `IUserRepository`, `IFolderRepository`, `IFileRepository`,
  `IRefreshTokenRepository`.
- **`AddBusinessServices(IConfiguration)`** (`Business/BusinessRegistration.cs`):
  - binds `JwtOptions` from configuration,
  - `IPasswordHasher` → `BCryptPasswordHasher` (singleton),
  - `ITokenService` → `TokenService` (singleton),
  - `IHttpContextAccessor` (via `AddHttpContextAccessor`),
  - `ITenantContext` → `TenantContext` (scoped),
  - `IAuthService` → `AuthService` (scoped),
  - `ITenantService` → `TenantService` (scoped).

---

## Configuration

Configuration keys read by the application (see `appsettings.json`):

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:SecretKey` | JWT signing key (HMAC-SHA256) |
| `Jwt:Issuer` | Token issuer |
| `Jwt:Audience` | Token audience |
| `Jwt:AccessTokenExpiryMinutes` | Access-token lifetime (default `15`) |
| `Jwt:RefreshTokenExpiryDays` | Refresh-token lifetime (default `7`) |

> **Production secrets must never be committed to Git.**
> JWT signing keys, database credentials, and other secrets must be provided through
> environment variables (e.g. `Jwt__SecretKey`, `ConnectionStrings__DefaultConnection`),
> a secret manager, or the deployment environment. Values are intentionally omitted
> from this document.

---

## Running the project

Requires the **.NET 10 SDK** and a reachable PostgreSQL instance.

```bash
# 1. Restore dependencies
dotnet restore

# 2. Build
dotnet build

# 3. Run (Development profile listens on http://localhost:5104 and https://localhost:7277)
dotnet run
```

Provide `Jwt:SecretKey` and the connection string via environment variables or
user-secrets for local development rather than editing tracked files.

---

## Database Setup

The schema is plain SQL (no EF migrations):

1. Provision a **PostgreSQL 15+** database and configure
   `ConnectionStrings:DefaultConnection`.
2. Apply the schema with your preferred client, e.g.:

   ```bash
   psql "$DATABASE_URL" -f scripts/schema.sql
   ```

3. (Optional, development only) Load sample data with `scripts/seed_data.sql`. Note the
   placeholder password hashes described under [Password Security](#password-security).

Do not run EF Core migrations — this project does not use them.

---

## Testing

**No automated test project currently exists in the repository.** `dotnet test` finds
no tests to run.

---

## Development Notes

For future contributors:

- Keep controllers **thin**.
- Business rules belong in **services**, not controllers.
- Controllers must obtain the authenticated identity from **`ITenantContext`**.
- **Never trust client-supplied tenant IDs.**
- Do **not** access EF Core directly from controllers.
- Do **not** create a second JWT authentication configuration.
- Do **not** store raw refresh tokens (store only the SHA-256 hash).
- Do **not** expose password hashes.
- Do **not** introduce EF migrations unless the SQL-first architecture is intentionally
  changed.

---

## Current Status

**Implemented on this branch:**

- Authentication business services (`AuthService`, `TokenService`, `TenantService`)
- JWT access tokens
- Refresh token rotation / revocation (with reuse detection)
- BCrypt password hashing
- Multi-tenant context (`ITenantContext` / `TenantContext`)
- Tenant isolation (identity-derived tenant + DB consistency triggers)
- Authentication API controller (`AuthController`, 7 endpoints)
- Authorization policies (`SuperAdmin`, `TenantAdmin`, `User`)
- OpenAPI Bearer security definition
- SQL `refresh_tokens` schema + EF mapping
- DI registration for the above

**Not implemented yet:**

- File, folder, user, storage, and S3 services (interfaces only)
- Controllers other than `AuthController`
- S3/object storage integration (no AWS SDK)
- Automated tests
- Swagger UI (only the OpenAPI document is served)

This branch is **not** claimed to be production-ready.
