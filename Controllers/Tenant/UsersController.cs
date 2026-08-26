using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Controllers
{
    [ApiController]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    [Route("api/v1/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetByTenantAsync(CancellationToken cancellationToken)
        {
            if (!TryGetTenantId(out var tenantId))
                return Unauthorized();

            var response = await _userService.GetUsersByTenantAsync(tenantId, cancellationToken);
            return Ok(response);
        }

        [HttpPost]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        public async Task<ActionResult<UserResponse>> CreateUserAsync(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetTenantId(out var tenantId))
                return Unauthorized();

            var response = await _userService.CreateUserAsync(tenantId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }

        [HttpPatch("{id:guid}")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserResponse>> UpdateUserAsync(
            Guid id,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetTenantId(out var tenantId))
                return Unauthorized();

            var response = await _userService.UpdateUserAsync(id, tenantId, request, cancellationToken);
            return Ok(response);
        }

        private bool TryGetTenantId(out Guid tenantId)
        {
            return Guid.TryParse(User.FindFirstValue("tenant_id"), out tenantId);
        }
    }
}