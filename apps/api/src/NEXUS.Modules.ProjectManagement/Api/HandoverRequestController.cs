using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/handover-requests")]
[Authorize]
public class HandoverRequestController : ControllerBase
{
    private readonly IProjectHandoverRequestService _handoverRequestService;
    private readonly IManagementContextAccessor? _managementContext;

    public HandoverRequestController(IProjectHandoverRequestService handoverRequestService, IManagementContextAccessor? managementContext = null)
    {
        _handoverRequestService = handoverRequestService;
        _managementContext = managementContext;
    }

    [Authorize(Policy = "Handover.Approve")]
    [OrganizationScopedCollectionPermission("Handover.Approve")]
    [HttpGet("pending-for-me")]
    public async Task<IActionResult> GetPendingForMe(CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: false, out var userId);
        if (denied is not null) return denied;
        var result = await _handoverRequestService.GetPendingForMeAsync(userId, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status400BadRequest,
                new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetDetails(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: false, out var userId);
        if (denied is not null) return denied;
        var result = await _handoverRequestService.GetExistingRequestDetailsAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error?.StatusCode ?? StatusCodes.Status404NotFound,
                result.Error ?? Error.NotFound("This handover request is not available to the current user."));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: false, out var userId);
        if (denied is not null) return denied;
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

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
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

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProjectHandoverRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
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

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
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
