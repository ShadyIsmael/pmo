using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectSnapshotService _projectSnapshotService;
    private readonly IProjectHandoverRequestService _projectHandoverRequestService;

    public ProjectController(IProjectService projectService, IProjectSnapshotService projectSnapshotService, IProjectHandoverRequestService projectHandoverRequestService)
    {
        _projectService = projectService;
        _projectSnapshotService = projectSnapshotService;
        _projectHandoverRequestService = projectHandoverRequestService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectService.GetPagedAsync(page, pageSize, status, priority, portfolioId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.ArchiveAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(new { message = "Project archived successfully" });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.ReactivateAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(new { message = "Project reactivated successfully" });
    }

    [HttpGet("{id:guid}/team")]
    public async Task<IActionResult> GetTeam(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetTeamAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/manager-history")]
    public async Task<IActionResult> GetManagerHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetManagerHistoryAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/handover-requests")]
    public async Task<IActionResult> CreateHandoverRequest(Guid id, [FromBody] CreateProjectHandoverRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectHandoverRequestService.CreateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/handover-requests")]
    public async Task<IActionResult> GetHandoverRequests(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectHandoverRequestService.GetForProjectAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/snapshots")]
    public async Task<IActionResult> GetSnapshots(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectSnapshotService.GetSnapshotsAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/snapshots/{snapshotId:guid}")]
    public async Task<IActionResult> GetSnapshot(Guid id, Guid snapshotId, CancellationToken cancellationToken)
    {
        var result = await _projectSnapshotService.GetSnapshotAsync(id, snapshotId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/team")]
    public async Task<IActionResult> AddTeamMember(Guid id, [FromBody] CreateProjectMemberDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        dto.ProjectId = id;
        var result = await _projectService.AddTeamMemberAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetTeam), new { id }, result.Value);
    }

    [HttpPut("{id:guid}/team/{memberId:guid}")]
    public async Task<IActionResult> UpdateTeamMember(Guid id, Guid memberId, [FromBody] UpdateProjectMemberDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.UpdateTeamMemberAsync(id, memberId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}/team/{memberId:guid}")]
    public async Task<IActionResult> RemoveTeamMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.RemoveTeamMemberAsync(id, memberId, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }

    [HttpGet("{id:guid}/team/conflicts")]
    public async Task<IActionResult> CheckResourceConflicts(
        Guid id,
        [FromQuery] Guid resourceId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] decimal allocationPercentage,
        [FromQuery] Guid? excludeMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectService.CheckResourceConflictsAsync(resourceId, startDate, endDate, allocationPercentage, excludeMemberId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }
}