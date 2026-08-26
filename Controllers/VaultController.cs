using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace miniDriveBackend.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/v1/vault")]
public class VaultController : ControllerBase
{
    [HttpPost("verify")]
    public IActionResult VerifyVaultPinAsync([FromBody] VerifyPinRequest request)
    {
        // Future integration point: inject IVaultService and let Business
        // validate the authenticated user, PIN hash, and unlock expiration.
        return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Vault is not implemented",
            Detail = "Vault PIN verification is not available yet."
        });
    }

    [HttpGet("files")]
    public IActionResult GetVaultFilesAsync()
    {
        // Future integration point: inject IVaultService and obtain files only
        // after the current user has a valid, non-expired vault session.
        return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Vault is not implemented",
            Detail = "Vault file listing is not available yet."
        });
    }
}

public sealed record VerifyPinRequest(string Pin);