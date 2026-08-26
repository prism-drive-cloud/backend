using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using miniDriveBackend.Business.Configuration;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Exceptions;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Data;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;
using UnauthorizedAccessException = miniDriveBackend.Business.Exceptions.UnauthorizedAccessException;

namespace miniDriveBackend.Business.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly AppDbContext _dbContext;
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            ITenantRepository tenantRepository,
            IRefreshTokenRepository refreshTokenRepository,
            ITokenService tokenService,
            IPasswordHasher passwordHasher,
            AppDbContext dbContext,
            IOptions<JwtOptions> jwtOptions,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _dbContext = dbContext;
            _jwtOptions = jwtOptions.Value;
            _logger = logger;
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

            if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for {Email}", email);
                throw new InvalidCredentialsException();
            }

            if (!user.IsActive)
                throw new InvalidCredentialsException("This account has been deactivated.");

            var tenant = await ResolveTenantForUserAsync(user, cancellationToken);
            return await IssueAuthResponseAsync(user, tenant, cancellationToken);
        }

        public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default)
        {
            var slug = SlugNormalizer.Normalize(request.Slug);
            if (!SlugNormalizer.IsValid(slug))
                throw new ArgumentException("Slug must contain only lowercase letters, numbers and hyphens.", nameof(request));

            var email = NormalizeEmail(request.AdminEmail);

            if (await _tenantRepository.ExistsBySlugAsync(slug, cancellationToken))
                throw new DuplicateResourceException("Tenant", "slug", slug);

            if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
                throw new DuplicateResourceException("User", "email", email);

            Tenant tenant;
            User admin;

            // Tenant + admin must be created atomically: a tenant must never persist without its admin.
            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    tenant = await _tenantRepository.CreateAsync(new Tenant
                    {
                        Name = request.TenantName.Trim(),
                        Slug = slug,
                        IsPersonal = false
                    }, cancellationToken);

                    admin = await _userRepository.CreateAsync(new User
                    {
                        TenantId = tenant.Id,
                        Email = email,
                        PasswordHash = _passwordHasher.Hash(request.AdminPassword),
                        FullName = request.AdminFullName.Trim(),
                        Role = UserRole.TenantAdmin,
                        IsActive = true
                    }, cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            _logger.LogInformation("Tenant {TenantId} registered with admin {UserId}", tenant.Id, admin.Id);
            return await IssueAuthResponseAsync(admin, tenant, cancellationToken);
        }

        public async Task<AuthResponse> RegisterUserAsync(RegisterUserRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
        {
            var currentUser = await _userRepository.GetByIdAsync(currentUserId, cancellationToken)
                ?? throw new UserNotFoundException(currentUserId);

            if (currentUser.Role == UserRole.User)
                throw new UnauthorizedAccessException("You are not allowed to create users.");

            var email = NormalizeEmail(request.Email);
            var targetRole = request.Role;
            Guid? targetTenantId;

            if (currentUser.Role == UserRole.TenantAdmin)
            {
                if (!currentUser.TenantId.HasValue)
                    throw new UnauthorizedAccessException("Tenant administrator is not associated with a tenant.");

                // A TenantAdmin can never create a SuperAdmin, and the tenant is always taken
                // from the caller's identity — never from client-supplied data.
                if (targetRole == UserRole.SuperAdmin)
                    throw new UnauthorizedAccessException("Tenant administrators cannot create super administrators.");

                targetTenantId = currentUser.TenantId;
            }
            else // SuperAdmin
            {
                if (targetRole != UserRole.SuperAdmin)
                {
                    // RegisterUserRequest carries no tenant. Creating tenant-scoped users is the job of
                    // IUserService.CreateUserAsync(tenantId, ...); this self-service path only mints SuperAdmins.
                    throw new ArgumentException(
                        "Creating a tenant-scoped user requires an explicit tenant; use the tenant user management endpoint.",
                        nameof(request));
                }

                targetTenantId = null;
            }

            if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
                throw new DuplicateResourceException("User", "email", email);

            var user = await _userRepository.CreateAsync(new User
            {
                TenantId = targetTenantId,
                Email = email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                FullName = request.FullName.Trim(),
                Role = targetRole,
                IsActive = true
            }, cancellationToken);

            _logger.LogInformation("User {UserId} ({Role}) created by {CreatorId}", user.Id, user.Role, currentUserId);

            var tenant = user.TenantId.HasValue
                ? await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken)
                : null;

            return await IssueAuthResponseAsync(user, tenant, cancellationToken);
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidCredentialsException("Invalid refresh token.");

            var tokenHash = HashRefreshToken(refreshToken);
            var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (stored is null)
                throw new InvalidCredentialsException("Invalid refresh token.");

            // Reuse detection: presenting an already-revoked token signals theft/replay.
            if (stored.RevokedAt is not null)
            {
                _logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all active tokens.", stored.UserId);
                await _refreshTokenRepository.RevokeAllActiveForUserAsync(stored.UserId, cancellationToken);
                throw new InvalidCredentialsException("Invalid refresh token.");
            }

            if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidCredentialsException("Refresh token has expired.");

            var user = await _userRepository.GetByIdAsync(stored.UserId, cancellationToken);
            if (user is null || !user.IsActive)
                throw new InvalidCredentialsException("Invalid refresh token.");

            var tenant = await ResolveTenantForUserAsync(user, cancellationToken);

            // Rotation: issue a new pair, then revoke the presented token and link the replacement.
            var accessToken = _tokenService.GenerateAccessToken(user);
            var rawRefreshToken = _tokenService.GenerateRefreshToken();
            var accessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);

            var newToken = await _refreshTokenRepository.CreateAsync(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(rawRefreshToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays)
            }, cancellationToken);

            stored.RevokedAt = DateTimeOffset.UtcNow;
            stored.ReplacedByTokenId = newToken.Id;
            await _refreshTokenRepository.UpdateAsync(stored, cancellationToken);

            return new AuthResponse(
                accessToken,
                rawRefreshToken,
                accessExpiresAt,
                MapToProfile(user),
                tenant is null ? null : MapToTenant(tenant));
        }

        public async Task<UserProfileResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new UserNotFoundException(userId);
            return MapToProfile(user);
        }

        public async Task<bool> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
            if (user is null || !user.IsActive)
                return false;

            return _passwordHasher.Verify(password, user.PasswordHash);
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new UserNotFoundException(userId);

            if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
                throw new InvalidCredentialsException("Current password is incorrect.");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
                throw new ArgumentException("New password must be at least 8 characters long.", nameof(newPassword));

            user.PasswordHash = _passwordHasher.Hash(newPassword);
            await _userRepository.UpdateAsync(user, cancellationToken);

            // Invalidate all existing sessions after a password change.
            await _refreshTokenRepository.RevokeAllActiveForUserAsync(userId, cancellationToken);

            _logger.LogInformation("Password changed for user {UserId}", userId);
            return true;
        }

        public async Task RevokeRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await _refreshTokenRepository.RevokeAllActiveForUserAsync(userId, cancellationToken);
        }

        private async Task<Tenant?> ResolveTenantForUserAsync(User user, CancellationToken cancellationToken)
        {
            if (user.Role == UserRole.SuperAdmin)
            {
                // SuperAdmin operates globally; the tenant may legitimately be null.
                return user.TenantId.HasValue
                    ? await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken)
                    : null;
            }

            if (!user.TenantId.HasValue)
                throw new UnauthorizedAccessException("User is not associated with a tenant.");

            return await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken)
                ?? throw new TenantNotFoundException(user.TenantId.Value);
        }

        private async Task<AuthResponse> IssueAuthResponseAsync(User user, Tenant? tenant, CancellationToken cancellationToken)
        {
            var accessToken = _tokenService.GenerateAccessToken(user);
            var rawRefreshToken = _tokenService.GenerateRefreshToken();
            var accessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);

            await _refreshTokenRepository.CreateAsync(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(rawRefreshToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays)
            }, cancellationToken);

            return new AuthResponse(
                accessToken,
                rawRefreshToken,
                accessExpiresAt,
                MapToProfile(user),
                tenant is null ? null : MapToTenant(tenant));
        }

        private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static string HashRefreshToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static UserProfileResponse MapToProfile(User user) =>
            new(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAt);

        private static TenantResponse MapToTenant(Tenant tenant) =>
            new(tenant.Id, tenant.Name, tenant.Slug, tenant.IsPersonal, tenant.StorageQuotaBytes, tenant.CreatedAt);
    }
}
