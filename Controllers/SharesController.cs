using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace miniDriveBackend.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/files/{id:guid}/share")]
public class SharesController : ControllerBase
{
    [HttpPost]
    public IActionResult CreateShareLinkAsync(Guid id)
    {
        // Future integration point: inject IShareService and delegate the
        // tenant, permission, expiration, and S3 link validation to Business.
        return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Sharing is not implemented",
            Detail = $"Share link creation for file '{id}' is not available yet."
        });
    }
}