using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;

namespace miniDriveBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ITenantContext _tenantContext;

        public AuthController(IAuthService authService, ITenantContext tenantContext)
        {
            _authService = authService;
            _tenantContext = tenantContext;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpPost("register-tenant")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        public async Task<ActionResult<AuthResponse>> RegisterTenant([FromBody] RegisterTenantRequest request, CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterTenantAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }

        [HttpPost("register-user")]
        [Authorize(Policy = "TenantAdmin")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        public async Task<ActionResult<AuthResponse>> RegisterUser([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
        {
            // The tenant/authorization scope is derived from the authenticated caller, never from the request.
            var currentUserId = _tenantContext.UserId;
            if (currentUserId is null)
                return Unauthorized();

            var response = await _authService.RegisterUserAsync(request, currentUserId.Value, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken cancellationToken)
        {
            var currentUserId = _tenantContext.UserId;
            if (currentUserId is null)
                return Unauthorized();

            var response = await _authService.GetCurrentUserAsync(currentUserId.Value, cancellationToken);
            return Ok(response);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] TokenRefreshRequest request, CancellationToken cancellationToken)
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Ok(response);
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var currentUserId = _tenantContext.UserId;
            if (currentUserId is null)
                return Unauthorized();

            await _authService.RevokeRefreshTokenAsync(currentUserId.Value, cancellationToken);
            return NoContent();
        }

        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            var currentUserId = _tenantContext.UserId;
            if (currentUserId is null)
                return Unauthorized();

            await _authService.ChangePasswordAsync(currentUserId.Value, request.CurrentPassword, request.NewPassword, cancellationToken);
            return Ok();
        }
    }
}
