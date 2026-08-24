# User Repository

## Interface: `IUserRepository`

**Location:** `Data/Interfaces/IUserRepository.cs`

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetSuperAdminsAsync(CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
```

### Method Details

| Method | Purpose | MVP Endpoint |
|--------|---------|--------------|
| `GetByIdAsync` | User by UUID (no tenant filter) | `GET /auth/me`, token refresh |
| `GetByEmailAsync` | User by email (login) | `POST /auth/login` |
| `GetByIdAndTenantAsync` | User by ID + tenant (isolation) | Any tenant-scoped operation |
| `GetByTenantIdAsync` | All users in tenant | Admin user management UI |
| `GetSuperAdminsAsync` | Global SuperAdmins (no tenant) | Platform admin features |
| `CreateAsync` | Insert new user | `POST /auth/register-user`, corporate invite |
| `UpdateAsync` | Update user (profile, role, status) | Profile edit, admin actions |
| `ExistsByEmailAsync` | Email uniqueness check | Registration validation |
| `ExistsByIdAndTenantAsync` | Ownership validation | Before file/folder operations |

---

## Implementation: `UserRepository`

**Location:** `Data/Repositories/UserRepository.cs`

```csharp
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetSuperAdminsAsync(CancellationToken ct = default)
    {
        return await DbSet
            .Where(u => u.Role == UserRole.SuperAdmin && u.TenantId == null)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public override async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        await DbSet.AddAsync(user, ct);
        await Context.SaveChangesAsync(ct);
        return user;
    }

    public override async Task<User> UpdateAsync(User user, CancellationToken ct = default)
    {
        DbSet.Update(user);
        await Context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(u => u.Id == id && u.TenantId == tenantId, ct);
    }
}
```

---

## Key Implementation Notes

### 1. Three Lookup Patterns for Different Contexts

```csharp
// 1. Global lookup (login, token refresh) - NO tenant filter
GetByIdAsync(id)
GetByEmailAsync(email)

// 2. Tenant-scoped lookup (standard operations) - REQUIRES tenantId
GetByIdAndTenantAsync(id, tenantId)
GetByTenantIdAsync(tenantId)
ExistsByIdAndTenantAsync(id, tenantId)

// 3. SuperAdmin global access - NO tenant filter, role-based
GetSuperAdminsAsync()
```

### 2. SuperAdmin Handling (Per Decision #3)

```csharp
// SuperAdmins have TenantId = NULL (enforced by DB CHECK constraint)
.Where(u => u.Role == UserRole.SuperAdmin && u.TenantId == null)
```

Service layer: if JWT `role == SuperAdmin`, call `GetSuperAdminsAsync()` or skip tenant checks.

### 3. Email Uniqueness: DB + App Level
- **DB**: Unique index `uq_users_email`
- **App**: `ExistsByEmailAsync` before registration

### 4. Corporate User Creation Flow
```csharp
// Admin creates user directly (no invitations table per Decision #6)
var user = new User
{
    TenantId = adminTenantId,  // From admin's JWT
    Email = "newuser@company.com",
    PasswordHash = hashedTempPassword,
    FullName = "New User",
    Role = UserRole.User
};
await _users.CreateAsync(user);
// Send notification email as side effect
```

---

## Usage Examples

```csharp
// Login: find by email, verify password hash
public async Task<User?> AuthenticateAsync(string email, string passwordHash)
{
    var user = await _users.GetByEmailAsync(email);
    if (user == null || !user.IsActive) return null;
    return VerifyPassword(passwordHash, user.PasswordHash) ? user : null;
}

// Register personal account
public async Task<User> RegisterPersonalAsync(string email, string passwordHash, string fullName)
{
    if (await _users.ExistsByEmailAsync(email))
        throw new ConflictException("Email already registered");

    // Personal tenant created separately, then linked
    var tenant = await _tenants.CreateAsync(new Tenant { ... });
    
    var user = new User
    {
        TenantId = tenant.Id,
        Email = email,
        PasswordHash = passwordHash,
        FullName = fullName,
        Role = UserRole.User
    };
    return await _users.CreateAsync(user);
}

// Corporate admin invites user
public async Task<User> InviteUserAsync(Guid adminTenantId, string email, string fullName)
{
    if (await _users.ExistsByEmailAsync(email))
        throw new ConflictException("Email already registered");

    var user = new User
    {
        TenantId = adminTenantId,
        Email = email,
        PasswordHash = GenerateTempPasswordHash(),
        FullName = fullName,
        Role = UserRole.User
    };
    return await _users.CreateAsync(user);
}

// Validate user belongs to tenant before file operation
public async Task<bool> ValidateOwnershipAsync(Guid userId, Guid tenantId)
{
    return await _users.ExistsByIdAndTenantAsync(userId, tenantId);
}
```

---

## Mapping to Database

| Property | Column | Notes |
|----------|--------|-------|
| `Id` | `id` | UUID, `gen_random_uuid()` |
| `TenantId` | `tenant_id` | UUID, FK → tenants, **NULL for SuperAdmin** |
| `Email` | `email` | Required, unique, max 255 |
| `PasswordHash` | `password_hash` | Required, BCrypt/Argon2 |
| `FullName` | `full_name` | Required, max 255 |
| `Role` | `role` | TEXT enum: `super_admin`, `tenant_admin`, `user` |
| `IsActive` | `is_active` | Default `true` |
| `CreatedAt` | `created_at` | `now()` default |
| `UpdatedAt` | `updated_at` | Trigger-maintained |

**Constraints:**
- `chk_users_role`: Role must be valid enum value
- `chk_users_super_admin_no_tenant`: SuperAdmin → `tenant_id` NULL; others → NOT NULL

See [Database Schema](../database-schema.md#users) and [Entity Models](../entity-models.md#user).