using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Authorize]
public class ProjectChangeRequestController : ControllerBase
{
    private readonly IProjectChangeRequestService _projectChangeRequestService;

    public ProjectChangeRequestController(IProjectChangeRequestService projectChangeRequestService)
    {
        _projectChangeRequestService = projectChangeRequestService;
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/projects/{projectId:guid}/change-requests")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        dto.ProjectId = projectId;
        var result = await _projectChangeRequestService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
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
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectChangeRequestService.ApproveAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Approve")]
    [HttpPost("api/project-change-requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProjectChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectChangeRequestService.RejectAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "ChangeRequest.Manage")]
    [HttpPost("api/project-change-requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectChangeRequestService.CancelAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);
        return Ok(result.Value);
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
