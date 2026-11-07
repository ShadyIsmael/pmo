using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/handover-requests")]
[Authorize]
public class HandoverRequestController : ControllerBase
{
    private readonly IProjectHandoverRequestService _handoverRequestService;

    public HandoverRequestController(IProjectHandoverRequestService handoverRequestService)
    {
        _handoverRequestService = handoverRequestService;
    }

    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("pending-for-me")]
    public async Task<IActionResult> GetPendingForMe(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _handoverRequestService.GetPendingForMeAsync(userId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.View")]
    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetDetails(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _handoverRequestService.GetExistingRequestDetailsAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status404NotFound,
                result.Error ?? Error.NotFound("This handover request is not available to the current user."));
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _handoverRequestService.GetApprovalReviewAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(new { error = result.Error.Message }),
                403 => Forbid(),
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _handoverRequestService.AcceptAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                _ => Conflict(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProjectHandoverRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _handoverRequestService.RejectAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.View")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _handoverRequestService.CancelAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }
}
