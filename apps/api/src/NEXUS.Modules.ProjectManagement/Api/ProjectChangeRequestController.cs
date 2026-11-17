using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Authorize]
public class ProjectChangeRequestController : ControllerBase
{
    private readonly IProjectChangeRequestService _projectChangeRequestService;
    private readonly IManagementContextAccessor? _managementContext;

    public ProjectChangeRequestController(IProjectChangeRequestService projectChangeRequestService, IManagementContextAccessor? managementContext = null)
    {
        _projectChangeRequestService = projectChangeRequestService;
        _managementContext = managementContext;
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/projects/{projectId:guid}/change-requests")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        dto.ProjectId = projectId;
        var result = await _projectChangeRequestService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpGet("api/projects/{projectId:guid}/change-requests/approvers")]
    public async Task<IActionResult> GetApprovers(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectChangeRequestService.GetApprovalOptionsAsync(projectId, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPut("api/project-change-requests/{id:guid}")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.UpdateDraftAsync(id, dto, userId, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/project-change-requests/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.SubmitAsync(id, dto, userId, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.View")]
    [HttpGet("api/projects/{projectId:guid}/change-requests")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectChangeRequestService.GetByProjectIdAsync(projectId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Approve")]
    [HttpGet("api/project-change-requests/pending-for-me")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var result = await _projectChangeRequestService.GetPendingAsync(cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Approve")]
    [HttpGet("api/project-change-requests/{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectChangeRequestService.GetApprovalReviewAsync(id, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.View")]
    [HttpGet("api/project-change-requests/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectChangeRequestService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Approve")]
    [HttpPost("api/project-change-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.ApproveAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(new ProjectChangeRequestDecisionDto(result.Value!.Id, result.Value.Number, result.Value.Status.ToString()));
    }

    [Authorize(Policy = "ChangeRequest.Approve")]
    [HttpPost("api/project-change-requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.RejectAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(new ProjectChangeRequestDecisionDto(result.Value!.Id, result.Value.Number, result.Value.Status.ToString()));
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/project-change-requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.CancelAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/project-change-requests/{id:guid}/remediate")]
    public async Task<IActionResult> Remediate(Guid id, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.RemediateLegacyAsync(id, userId, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/project-change-requests/{id:guid}/approvals/{assignmentId:guid}/reassign")]
    public async Task<IActionResult> Reassign(Guid id, Guid assignmentId, [FromBody] ReassignProjectChangeRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
        var result = await _projectChangeRequestService.ReassignApprovalAsync(id, assignmentId, dto, userId, cancellationToken);
        return result.IsFailure ? MapError(result.Error) : Ok(result.Value);
    }

    private IActionResult MapError(Error? error)
    {
        return error?.StatusCode switch
        {
            401 => Unauthorized(new { error = error.Message }),
            403 => Forbid(),
            404 => NotFound(new { error = error.Message }),
            409 => Conflict(new { error = error.Message }),
            _ => BadRequest(new { error = error?.Message })
        };
    }
}
