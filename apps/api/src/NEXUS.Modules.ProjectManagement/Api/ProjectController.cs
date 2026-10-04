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
    private readonly IProjectStatusReportService _projectStatusReportService;
    private readonly IProjectOverviewService _projectOverviewService;

    public ProjectController(IProjectService projectService, IProjectSnapshotService projectSnapshotService, IProjectHandoverRequestService projectHandoverRequestService, IProjectStatusReportService projectStatusReportService, IProjectOverviewService projectOverviewService)
    {
        _projectService = projectService;
        _projectSnapshotService = projectSnapshotService;
        _projectHandoverRequestService = projectHandoverRequestService;
        _projectStatusReportService = projectStatusReportService;
        _projectOverviewService = projectOverviewService;
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectOverviewService.GetAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}/shell")]
    public async Task<IActionResult> GetShell(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetShellAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
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

    [Authorize(Policy = "Project.View")]
    [HttpGet("{projectId:guid}/status-reports")]
    public async Task<IActionResult> GetStatusReports(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _projectStatusReportService.GetPagedAsync(projectId, page, pageSize, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{projectId:guid}/status-reports/current-progress")]
    public async Task<IActionResult> GetCurrentStatusReportProgress(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectStatusReportService.GetCurrentProgressAsync(projectId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> GetStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var result = await _projectStatusReportService.GetByIdAsync(projectId, reportId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.Edit")]
    [HttpPost("{projectId:guid}/status-reports")]
    public async Task<IActionResult> CreateStatusReport(Guid projectId, [FromBody] CreateProjectStatusReportDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectStatusReportService.CreateAsync(projectId, dto, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode switch { 404 => NotFound(new { error = result.Error.Message }), _ => BadRequest(new { error = result.Error?.Message }) };
        return CreatedAtAction(nameof(GetStatusReport), new { projectId, reportId = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Project.Edit")]
    [HttpPut("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> UpdateStatusReport(Guid projectId, Guid reportId, [FromBody] UpdateProjectStatusReportDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectStatusReportService.UpdateAsync(projectId, reportId, dto, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode switch { 404 => NotFound(new { error = result.Error.Message }), 409 => Conflict(new { error = result.Error.Message }), _ => BadRequest(new { error = result.Error?.Message }) };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.Edit")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/publish")]
    public async Task<IActionResult> PublishStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectStatusReportService.PublishAsync(projectId, reportId, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode switch { 404 => NotFound(new { error = result.Error.Message }), 409 => Conflict(new { error = result.Error.Message }), _ => BadRequest(new { error = result.Error?.Message }) };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.Edit")]
    [HttpDelete("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> DeleteStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectStatusReportService.DeleteAsync(projectId, reportId, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode switch { 404 => NotFound(new { error = result.Error.Message }), 409 => Conflict(new { error = result.Error.Message }), _ => BadRequest(new { error = result.Error?.Message }) };
        return NoContent();
    }

    [Authorize(Policy = "Project.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Project.Edit")]
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

    [Authorize(Policy = "Project.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }

    [Authorize(Policy = "Project.Edit")]
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

    [Authorize(Policy = "Project.Edit")]
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

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}/team")]
    public async Task<IActionResult> GetTeam(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetTeamAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}/manager-history")]
    public async Task<IActionResult> GetManagerHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetManagerHistoryAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Manage")]
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

    [Authorize(Policy = "Handover.View")]
    [HttpGet("{id:guid}/handover-requests")]
    public async Task<IActionResult> GetHandoverRequests(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectHandoverRequestService.GetForProjectAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}/snapshots")]
    public async Task<IActionResult> GetSnapshots(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectSnapshotService.GetSnapshotsAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
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

    [Authorize(Policy = "Resource.Assign")]
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

    [Authorize(Policy = "Resource.Assign")]
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

    [Authorize(Policy = "Resource.Assign")]
    [HttpDelete("{id:guid}/team/{memberId:guid}")]
    public async Task<IActionResult> RemoveTeamMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.RemoveTeamMemberAsync(id, memberId, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }

    [Authorize(Policy = "Project.View")]
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
