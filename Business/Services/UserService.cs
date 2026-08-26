using miniDriveBackend.Business.Exceptions;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<UserResponse> CreateUserAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            {
                _logger.LogWarning("Intento de crear usuario con email duplicado {Email}", request.Email);
                throw new DuplicateResourceException("User", "email", request.Email);
            }

            var user = new User
            {
                TenantId = tenantId,
                Email = request.Email,
                FullName = request.FullName,
                Role = request.Role,
                IsActive = true
            };
            user.PasswordHash = _passwordHasher.Hash(request.Password);

            var created = await _userRepository.CreateAsync(user, cancellationToken);
            _logger.LogInformation("Usuario {UserId} creado para tenant {TenantId}", created.Id, tenantId);

            return ToResponse(created);
        }

        public async Task<IReadOnlyList<UserResponse>> GetUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var users = await _userRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            return users.Select(ToResponse).ToList();
        }

        public async Task<UserResponse?> GetUserByIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAndTenantAsync(userId, tenantId, cancellationToken);
            return user is null ? null : ToResponse(user);
        }

        public async Task<UserResponse> UpdateUserAsync(Guid userId, Guid tenantId, UpdateUserRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var user = await _userRepository.GetByIdAndTenantAsync(userId, tenantId, cancellationToken)
                ?? throw new UserNotFoundException(userId);

            if (!string.IsNullOrWhiteSpace(request.FullName))
                user.FullName = request.FullName;

            if (request.Role.HasValue)
                user.Role = request.Role.Value;

            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            var updated = await _userRepository.UpdateAsync(user, cancellationToken);
            _logger.LogInformation("Usuario {UserId} actualizado", userId);

            return ToResponse(updated);
        }

        public async Task<bool> DeactivateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAndTenantAsync(userId, tenantId, cancellationToken)
                ?? throw new UserNotFoundException(userId);

            if (!user.IsActive)
                return true;

            user.IsActive = false;
            await _userRepository.UpdateAsync(user, cancellationToken);
            _logger.LogInformation("Usuario {UserId} desactivado", userId);

            return true;
        }

        public async Task<bool> ActivateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAndTenantAsync(userId, tenantId, cancellationToken)
                ?? throw new UserNotFoundException(userId);

            if (user.IsActive)
                return true;

            user.IsActive = true;
            await _userRepository.UpdateAsync(user, cancellationToken);
            _logger.LogInformation("Usuario {UserId} activado", userId);

            return true;
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => _userRepository.ExistsByEmailAsync(email, cancellationToken);

        public Task<bool> ExistsByIdAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => _userRepository.ExistsByIdAndTenantAsync(userId, tenantId, cancellationToken);

        private static UserResponse ToResponse(User user) => new(
            user.Id,
            user.TenantId,
            user.Email,
            user.FullName,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}