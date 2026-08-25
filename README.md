# miniDriveBackend

## Project Overview

`miniDriveBackend` is the backend for a multi-tenant "mini drive" file-storage
application, built on **.NET 10** (ASP.NET Core) with **PostgreSQL**.

The current branch implements the **authentication and multi-tenant
security/business infrastructure**: login, tenant/user registration, JWT access
tokens, refresh tokens with rotation and reuse detection, BCrypt password
hashing, and a request-scoped tenant context for enforcing tenant isolation.

> **Status:** The auth/multi-tenancy business + security layer is implemented.
> HTTP controllers/endpoints for authentication are **not implemented yet**
> (see [Current Scope](#current-scope)). File, folder, user, storage and S3
> operations currently exist only as **interface contracts** — no
> implementations.

---

## Architecture

The solution is a single ASP.NET Core project organized into layers by folder.

| Layer | Location | Responsibility |
|-------|----------|----------------|
| **API / host** | `Program.cs` | App bootstrap: DI wiring, JWT authentication, authorization policies, EF Core `DbContext`, request pipeline. Also exposes the default OpenAPI + demo endpoint. No auth controllers yet. |
| **Business** | `Business/` | Service interfaces, DTOs, custom exceptions, and the implemented services (`AuthService`, `TokenService`, `TenantService`), security helpers, configuration options, and DI registration. |
| **Models** | `Models/` | EF Core entities: `Tenant`, `User`, `Folder`, `FileEntity`, `RefreshToken`, plus `BaseEntity` and the `UserRole` enum. |
| **Data / Repositories** | `Data/` | `AppDbContext` (EF Core model configuration) and the repository pattern (`Data/Repositories/` + `Data/Interfaces/`). |
| **PostgreSQL** | `scripts/schema.sql` | The database schema (SQL-first source of truth), including multi-tenant consistency triggers. |

### Storage / S3 (not implemented)

`Business/Interfaces/IS3Service.cs` and `Business/Interfaces/IStorageService.cs`
define **contracts** for presigned-URL storage and quota enforcement, but there
are **no implementations** and no AWS SDK dependency in the project yet. They are
documented here only so contributors know the intended shape; do not assume S3
functionality is available.

### Business services: implemented vs contract-only

- **Implemented:** `IAuthService` → `AuthService`, `ITokenService` →
  `TokenService`, `ITenantService` → `TenantService`.
- **Contract-only (interfaces, no implementation):** `IUserService`,
  `IFileService`, `IFolderService`, `IStorageService`, `IS3Service`.

---

## Authentication

The authentication infrastructure lives in `Business/Services/`,
`Business/Security/`, `Business/Interfaces/`, and `Business/Configuration/`.

| Component | File | Responsibility |
|-----------|------|----------------|
| **AuthService** | `Business/Services/AuthService.cs` | Orchestrates login, tenant registration, user registration, token refresh, current-user lookup, credential validation, password change, and refresh-token revocation. Enforces role/tenant rules. |
| **TokenService** | `Business/Services/TokenService.cs` | Generates and validates signed JWT access tokens; generates cryptographically random refresh tokens. Uses `Microsoft.IdentityModel.JsonWebTokens`. |
| **BCryptPasswordHasher** | `Business/Security/BCryptPasswordHasher.cs` | `IPasswordHasher` implementation using `BCrypt.Net-Next` for hashing and verification. |
| **IPasswordHasher** | `Business/Interfaces/IPasswordHasher.cs` | Abstraction over password hashing (`Hash`, `Verify`) so the algorithm is swappable and services stay testable. |
| **ITenantContext** | `Business/Interfaces/ITenantContext.cs` | Exposes the authenticated identity: `UserId`, `TenantId`, `Role`, `IsAuthenticated`. |
| **TenantContext** | `Business/Security/TenantContext.cs` | `ITenantContext` implementation that reads `sub` / `tenant_id` / `role` claims from `IHttpContextAccessor.HttpContext.User`. Request-scoped. |

---

## Authentication Flow

There are no HTTP endpoints yet — the flows below describe the **service-level**
behavior of `AuthService`.

### Login (`LoginAsync`)

```
Look up user by (normalized) email
  → BCrypt verify password
  → verify the account is active
  → resolve tenant:
        SuperAdmin  → tenant may be null
        other roles → TenantId must exist and the tenant must exist
  → generate JWT access token
  → generate refresh token (store only its SHA-256 hash)
  → return AuthResponse (access token, raw refresh token, user, tenant?)
```

Invalid email, wrong password, and inactive accounts all surface as
`InvalidCredentialsException` (to avoid user enumeration).

### Tenant registration (`RegisterTenantAsync`)

Validates and normalizes the slug, checks slug + admin-email uniqueness, then
creates the `Tenant` and its `TenantAdmin` user **inside a database transaction**
so a tenant can never persist without its admin. Passwords are BCrypt-hashed.
Returns an `AuthResponse` for the new admin.

### User registration (`RegisterUserAsync`)

The tenant is derived from the **authenticated caller**, never from the request.
See [Multi-Tenancy](#multi-tenancy) and [Roles](#roles).

### Password change (`ChangePasswordAsync`)

Verifies the current password (BCrypt), enforces a minimum new-password length,
hashes the new password, updates the user, and **revokes all active refresh
tokens** for that user (existing sessions are invalidated).

### Refresh (`RefreshTokenAsync`)

```
Hash the presented refresh token (SHA-256)
  → look it up by hash
  → if not found or expired → reject
  → if already revoked → REUSE DETECTED: revoke the user's whole active chain, reject
  → re-load the user (must exist + be active); re-resolve tenant
  → issue a new access token + new refresh token
  → mark the old token RevokedAt = now and ReplacedByTokenId = new token id (rotation)
```

### Logout / revocation (`RevokeRefreshTokenAsync`)

Revokes all active refresh tokens for a user (server-side session termination).

### Refresh token rotation

Every successful refresh issues a brand-new refresh token and revokes the one
presented, linking them via `ReplacedByTokenId`. A refresh token is therefore
single-use.

### Refresh token reuse detection

If a token that has already been revoked (e.g. an old, rotated-out token) is
presented, this is treated as replay/theft: the user's entire active refresh
chain is revoked and the request is rejected.

---

## JWT

Access tokens are signed with **HMAC-SHA256** using the configured signing key.

**Claims emitted:**

| Claim | Value |
|-------|-------|
| `sub` | User id (GUID) |
| `role` | `SuperAdmin` / `TenantAdmin` / `User` (enum name) |
| `jti` | Unique token id (GUID) |
| `tenant_id` | User's tenant id — **only present when the user has a tenant** |
| `email`, `name` | User email and full name |
| `iss` / `aud` | Issuer / audience from configuration |
| `iat` / `nbf` / `exp` | Issued-at / not-before / expiry |

Notes:

- **Access tokens expire after 15 minutes** (`Jwt:AccessTokenExpiryMinutes`,
  default `15`).
- **`MapInboundClaims = false`** — inbound claim names are kept verbatim
  (`sub`, `role`, `tenant_id`), not remapped to legacy XML claim URIs.
- **`RoleClaimType = "role"`** and `NameClaimType = "sub"`, consistent between the
  JWT bearer middleware (`Program.cs`) and `TokenService`.
- **SuperAdmin may have no `tenant_id`** — the claim is omitted entirely rather
  than set to an empty/placeholder value.

No secrets are shown here; see [Configuration](#configuration).

---

## Roles

Roles are defined in `Models/UserRole.cs`: `SuperAdmin`, `TenantAdmin`, `User`.
Behavior below reflects the **actual implementation** in `AuthService` and the
authorization policies in `Program.cs`.

| Role | Tenant scope | User creation (`RegisterUserAsync`) |
|------|--------------|-------------------------------------|
| **SuperAdmin** | Global; `TenantId` may be `null` | May create other SuperAdmins. Creating tenant-scoped users is intended for `IUserService.CreateUserAsync(tenantId, …)`, which is not implemented yet, so this self-service path only mints SuperAdmins. |
| **TenantAdmin** | Bound to its own tenant | May create users **within its own tenant only**. Cannot create a SuperAdmin. Cannot choose a different tenant. |
| **User** | Own resources within its tenant | Cannot create users. |

**Authorization policies** are registered in `Program.cs` and available for
future controllers (they are not yet applied to any endpoint):

- `SuperAdmin` → requires role `SuperAdmin`
- `TenantAdmin` → requires role `SuperAdmin` or `TenantAdmin`
- `User` → requires role `SuperAdmin`, `TenantAdmin`, or `User`

---

## Multi-Tenancy

**The tenant is NEVER taken from a client-provided `tenant_id` value.**

The authenticated tenant flows in one direction:

```
   Client request (Authorization: Bearer <JWT>)
              │
              ▼
   JwtBearer middleware  ── validates signature / iss / aud / exp
              │
              ▼
   ClaimsPrincipal (sub, role, tenant_id)
              │
              ▼
   ITenantContext (TenantContext)   ← the ONLY trusted source of the tenant
              │
              ▼
   Business service                 ← passes the authenticated TenantId
              │
              ▼
   Repository (tenant-scoped query: WHERE ... AND tenant_id = @tenantId)
              │
              ▼
   PostgreSQL  ── tenant-consistency triggers (defense in depth)
```

Rules enforced / relied upon:

- **Normal users belong to exactly one tenant** (`User.TenantId`).
- **SuperAdmin may have `TenantId = null`** and operates globally.
- **TenantAdmin is restricted to its own tenant** — in `RegisterUserAsync` the
  new user's tenant is always taken from `currentUser.TenantId`.
- **TenantAdmin cannot create a SuperAdmin.**
- **TenantAdmin cannot select another tenant** — no request DTO carries a tenant
  id for these operations, so a foreign tenant cannot be injected.
- **Tenant-scoped repository operations must receive the authenticated
  `TenantId`.** Repositories already expose tenant-scoped methods such as
  `GetByIdAndTenantAsync` / `GetByIdAsync(id, tenantId)` /
  `ExistsByIdAndTenantAsync` (see `Data/Interfaces/`).
- **PostgreSQL tenant-consistency triggers** (`fn_validate_folder_tenant_consistency`,
  `fn_validate_file_tenant_consistency` in `scripts/schema.sql`) reject rows whose
  owner/folder do not belong to the row's tenant — defense in depth even if
  application code has a bug.

---

## Refresh Tokens

Refresh tokens are persisted in the `refresh_tokens` table.

- **Cryptographically random, 256-bit** token generated via
  `RandomNumberGenerator` and base64url-encoded (`TokenService.GenerateRefreshToken`).
- **Only the SHA-256 hash is persisted** (`token_hash`); the raw token is
  returned to the client exactly once and never stored.
- **Expiration** is set at creation (`Jwt:RefreshTokenExpiryDays`, default `7`)
  and checked on every refresh.
- **Rotation:** each refresh issues a new token and revokes the presented one.
- **`RevokedAt`** marks a token as no longer usable.
- **`ReplacedByTokenId`** links a rotated token to its successor.
- **Reuse detection:** presenting an already-revoked token revokes the user's
  entire active chain.
- **User/session revocation:** `RevokeRefreshTokenAsync` revokes all active
  tokens for a user.
- **Password changes revoke active sessions** (`ChangePasswordAsync`).

### `refresh_tokens` table

Defined in `scripts/schema.sql` and mirrored by the EF Core configuration in
`Data/AppDbContext.cs` (`ConfigureRefreshToken`):

| Column | Notes |
|--------|-------|
| `id` | UUID PK, `DEFAULT gen_random_uuid()` |
| `user_id` | UUID, `NOT NULL`, FK → `users(id)` `ON DELETE CASCADE` |
| `token_hash` | TEXT, `NOT NULL`, **UNIQUE** (`uq_refresh_tokens_token_hash`) |
| `expires_at` | TIMESTAMPTZ, `NOT NULL` |
| `revoked_at` | TIMESTAMPTZ, nullable |
| `replaced_by_token_id` | UUID, nullable, FK → `refresh_tokens(id)` `ON DELETE SET NULL` |
| `created_at` / `updated_at` | TIMESTAMPTZ, `DEFAULT now()`; `updated_at` maintained by trigger |

Indexes: `uq_refresh_tokens_token_hash` (unique), `idx_refresh_tokens_user_id`.

---

## Password Security

- Hashing uses **`BCrypt.Net-Next`** through `IPasswordHasher` /
  `BCryptPasswordHasher`.
- **Hashing** happens on tenant/user registration and password change;
  **verification** happens on login, credential validation, and password change —
  always through the same `IPasswordHasher`.
- **Password hashes are never returned in DTOs.** `UserProfileResponse`,
  `TenantResponse`, and `AuthResponse` contain no password material, and no
  password or hash is written to logs.
- ⚠️ **`scripts/seed_data.sql` currently contains placeholder BCrypt hashes.**
  They are not real credentials — replace them with hashes produced by the
  application's hasher before attempting to log in as any seeded user.

---

## Database

The project is **SQL-first**.

- **`scripts/schema.sql` is the single source of truth** for the database schema
  (PostgreSQL 15+). It defines tables, constraints, indexes, and the multi-tenant
  consistency triggers.
- **`refresh_tokens` was added to `scripts/schema.sql`** following the existing
  conventions.
- The **EF Core entities and `AppDbContext` configuration mirror the SQL schema**
  (column names, types, nullability, indexes, and foreign keys).
- **EF Core migrations are NOT used.** There is no `Migrations/` folder and no
  `Microsoft.EntityFrameworkCore.Design` dependency. Do not introduce migrations;
  change `scripts/schema.sql` and keep the EF configuration in sync.

Relevant constraints/keys (see `scripts/schema.sql`):

- `users.email` unique (`uq_users_email`); `role` is stored as
  `super_admin` / `tenant_admin` / `user` and mapped from the `UserRole` enum in
  `AppDbContext`. A check constraint enforces that only SuperAdmin may have a
  `NULL` tenant.
- `tenants.slug` unique (`uq_tenants_slug`).
- `files.s3_key` unique; `tenant_id` indexes on `users`, `folders`, `files`.
- Foreign keys from `users`, `folders`, `files`, and `refresh_tokens` to their
  parents, with cascade/restrict/set-null behavior as documented above.

---

## Dependency Injection

Registration is split into two extension methods:

- **`AddDataAccess()`** (`Data/DAORegistration.cs`) — registers the repositories
  as scoped: `ITenantRepository`, `IUserRepository`, `IFolderRepository`,
  `IFileRepository`, `IRefreshTokenRepository`.
- **`AddBusinessServices(IConfiguration)`** (`Business/BusinessRegistration.cs`)
  — binds `JwtOptions` from configuration and registers:
  - `IPasswordHasher` → `BCryptPasswordHasher` (singleton)
  - `ITokenService` → `TokenService` (singleton)
  - `IHttpContextAccessor` (via `AddHttpContextAccessor`)
  - `ITenantContext` → `TenantContext` (scoped)
  - `IAuthService` → `AuthService` (scoped)
  - `ITenantService` → `TenantService` (scoped)

Both are called from `Program.cs`, along with `AddDbContext<AppDbContext>` (Npgsql).

---

## Configuration

The application reads the following keys (see `appsettings.json`):

| Key | Purpose | Default |
|-----|---------|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | — |
| `Jwt:SecretKey` | JWT signing key (HMAC-SHA256) | — (placeholder in `appsettings.json`) |
| `Jwt:Issuer` | Token issuer | `mini-drive` |
| `Jwt:Audience` | Token audience | `mini-drive-clients` |
| `Jwt:AccessTokenExpiryMinutes` | Access-token lifetime | `15` |
| `Jwt:RefreshTokenExpiryDays` | Refresh-token lifetime | `7` |

> **Production secrets MUST NOT be committed to Git.**
> JWT signing keys and database credentials must be supplied through secure
> environment variables, a secret manager, or the deployment environment
> (e.g. .NET user-secrets for local development, or environment variables such as
> `Jwt__SecretKey` and `ConnectionStrings__DefaultConnection`). The value in
> `appsettings.json` is a non-secret placeholder and must be overridden.

---

## Current Scope

- ✅ **Implemented:** authentication and multi-tenant **business + security
  infrastructure** — `AuthService`, `TokenService`, `TenantService`,
  `BCryptPasswordHasher`, `TenantContext`, refresh-token persistence with
  rotation/reuse detection, JWT bearer authentication, and authorization
  policies.
- ❌ **Not implemented:** authentication **controllers/HTTP endpoints**, and the
  file/folder/user/storage/S3 services (interfaces only).

Future controllers must:

- use the authenticated **`ITenantContext`**;
- **never trust client-provided tenant IDs**;
- pass the authenticated **`TenantId` / `UserId`** into tenant-scoped business
  operations;
- rely on the existing **authorization policies** (`SuperAdmin`, `TenantAdmin`,
  `User`).

---

## Development

Requires the **.NET 10 SDK**.

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (Development profile listens on http://localhost:5104 and https://localhost:7277)
dotnet run
```

Running the app requires a reachable PostgreSQL instance (see
[Database Setup](#database-setup)) and a valid `Jwt:SecretKey`. For local
development, supply the signing key via user-secrets or environment variables
rather than editing tracked files.

---

## Testing

There is currently **no test project** in the repository. `dotnet test` finds no
tests to run. No automated tests exist yet.

---

## Database Setup

1. Provision a **PostgreSQL 15+** database and configure
   `ConnectionStrings:DefaultConnection` (via environment/user-secrets for
   secrets).
2. Apply the schema — it is plain SQL, applied with your preferred client, e.g.:

   ```bash
   psql "$DATABASE_URL" -f scripts/schema.sql
   ```

3. (Optional, development only) Load sample data with
   `scripts/seed_data.sql`. Note the placeholder password hashes described in
   [Password Security](#password-security).

There are no EF Core migrations; `scripts/schema.sql` is applied directly.

---

## Security Notes

Guarantees provided by the current implementation:

- **BCrypt password hashing** for all stored passwords.
- **Signed JWT** access tokens (HMAC-SHA256) with validated issuer/audience/lifetime.
- **Short-lived access tokens** (15 minutes by default).
- **Hashed refresh tokens** — only SHA-256 hashes are persisted.
- **Refresh token rotation** — refresh tokens are single-use.
- **Refresh token reuse detection** — replay revokes the user's active chain.
- **Tenant isolation** — the tenant is sourced only from the authenticated
  identity (`ITenantContext`), never from client input.
- **Privilege-escalation protection** — a TenantAdmin cannot create a SuperAdmin
  or act on another tenant.
- **SQL tenant-consistency triggers** — database-level defense in depth against
  cross-tenant rows.
