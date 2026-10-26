using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

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
    private readonly IProjectReportingService _projectReportingService;

    public ProjectController(IProjectService projectService, IProjectSnapshotService projectSnapshotService, IProjectHandoverRequestService projectHandoverRequestService, IProjectStatusReportService projectStatusReportService, IProjectOverviewService projectOverviewService, IProjectReportingService projectReportingService)
    {
        _projectService = projectService;
        _projectSnapshotService = projectSnapshotService;
        _projectHandoverRequestService = projectHandoverRequestService;
        _projectStatusReportService = projectStatusReportService;
        _projectOverviewService = projectOverviewService;
        _projectReportingService = projectReportingService;
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
    [Authorize(Policy = "Resource.View")]
    [HttpGet("{id:guid}/overview/team-capacity")]
    public async Task<IActionResult> GetTeamCapacityOverview(
        Guid id,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string periodType = "Month",
        CancellationToken cancellationToken = default)
    {
        var result = await _projectOverviewService.GetTeamCapacityAsync(id, startDate, endDate, periodType, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        }
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

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/status-reports")]
    public async Task<IActionResult> GetStatusReports(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _projectStatusReportService.GetPagedAsync(projectId, page, pageSize, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/status-reports/current-progress")]
    public async Task<IActionResult> GetCurrentStatusReportProgress(Guid projectId, [FromQuery] DateTime? statusDate, CancellationToken cancellationToken)
    {
        var result = await _projectStatusReportService.GetCurrentProgressAsync(projectId, statusDate, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> GetStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var result = await _projectStatusReportService.GetByIdAsync(projectId, reportId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/status-reports/compare")]
    public async Task<IActionResult> CompareStatusReports(
        Guid projectId,
        [FromQuery] Guid firstReportId,
        [FromQuery] Guid secondReportId,
        CancellationToken cancellationToken)
    {
        var result = await _projectStatusReportService.ComparePublishedReportsAsync(projectId, firstReportId, secondReportId, cancellationToken);
        if (result.IsFailure) return StatusReportError(result.Error!);
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.Create")]
    [HttpPost("{projectId:guid}/status-reports")]
    public async Task<IActionResult> CreateStatusReport(Guid projectId, [FromBody] CreateProjectStatusReportDto dto, CancellationToken cancellationToken)
    {
        var userId = GetStatusReportActorId();
        if (userId is null) return Unauthorized();
        var result = await _projectStatusReportService.CreateAsync(projectId, dto, userId, cancellationToken);
        if (result.IsFailure) return StatusReportError(result.Error!);
        return CreatedAtAction(nameof(GetStatusReport), new { projectId, reportId = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "StatusReport.Edit")]
    [HttpPut("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> UpdateStatusReport(Guid projectId, Guid reportId, [FromBody] UpdateProjectStatusReportDto dto, CancellationToken cancellationToken)
    {
        var userId = GetStatusReportActorId();
        if (userId is null) return Unauthorized();
        var result = await _projectStatusReportService.UpdateAsync(projectId, reportId, dto, userId, cancellationToken);
        if (result.IsFailure) return StatusReportError(result.Error!);
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.Generate")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/generate")]
    public Task<IActionResult> GenerateStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken) =>
        RunStatusReportTransition(projectId, reportId, "Generate", cancellationToken);

    [Authorize(Policy = "StatusReport.Submit")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/submit")]
    public Task<IActionResult> SubmitStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken) =>
        RunStatusReportTransition(projectId, reportId, "Submit", cancellationToken);

    [Authorize(Policy = "StatusReport.Approve")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/approve")]
    public Task<IActionResult> ApproveStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken) =>
        RunStatusReportTransition(projectId, reportId, "Approve", cancellationToken);

    [Authorize(Policy = "StatusReport.Approve")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/return")]
    public Task<IActionResult> ReturnStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken) =>
        RunStatusReportTransition(projectId, reportId, "Return", cancellationToken);

    [Authorize(Policy = "StatusReport.Edit")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/reopen")]
    public Task<IActionResult> ReopenStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken) =>
        RunStatusReportTransition(projectId, reportId, "Reopen", cancellationToken);

    [Authorize(Policy = "StatusReport.Publish")]
    [HttpPost("{projectId:guid}/status-reports/{reportId:guid}/publish")]
    public async Task<IActionResult> PublishStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        return await RunStatusReportTransition(projectId, reportId, "Publish", cancellationToken);
    }

    private async Task<IActionResult> RunStatusReportTransition(Guid projectId, Guid reportId, string transition, CancellationToken cancellationToken)
    {
        var userId = GetStatusReportActorId();
        if (userId is null) return Unauthorized();
        var result = transition switch
        {
            "Generate" => await _projectStatusReportService.GenerateAsync(projectId, reportId, userId, cancellationToken),
            "Submit" => await _projectStatusReportService.SubmitAsync(projectId, reportId, userId, cancellationToken),
            "Approve" => await _projectStatusReportService.ApproveAsync(projectId, reportId, userId, cancellationToken),
            "Return" => await _projectStatusReportService.ReturnAsync(projectId, reportId, userId, cancellationToken),
            "Reopen" => await _projectStatusReportService.ReopenAsync(projectId, reportId, userId, cancellationToken),
            "Publish" => await _projectStatusReportService.PublishAsync(projectId, reportId, userId, cancellationToken),
            _ => throw new InvalidOperationException("Unknown status report transition.")
        };
        if (result.IsFailure) return StatusReportError(result.Error!);
        return Ok(result.Value);
    }

    private string? GetStatusReportActorId()
    {
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(actorId) ? null : actorId;
    }

    private IActionResult StatusReportError(Error error) => error.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error.Message })
    };

    [Authorize(Policy = "StatusReport.Edit")]
    [HttpDelete("{projectId:guid}/status-reports/{reportId:guid}")]
    public async Task<IActionResult> DeleteStatusReport(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var userId = GetStatusReportActorId();
        if (userId is null) return Unauthorized();
        var result = await _projectStatusReportService.DeleteAsync(projectId, reportId, userId, cancellationToken);
        if (result.IsFailure) return StatusReportError(result.Error!);
        return NoContent();
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/reporting/options")]
    public async Task<IActionResult> GetReportingOptions(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectReportingService.GetOptionsAsync(projectId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/reporting/configuration")]
    public async Task<IActionResult> GetReportingConfiguration(Guid projectId, [FromQuery] DateTime? effectiveAt, CancellationToken cancellationToken)
    {
        var result = await _projectReportingService.GetConfigurationAsync(projectId, effectiveAt, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.ManageConfiguration")]
    [HttpPut("{projectId:guid}/reporting/configuration")]
    public async Task<IActionResult> SaveReportingConfiguration(Guid projectId, [FromBody] SaveProjectReportingConfigurationDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _projectReportingService.SaveConfigurationAsync(projectId, dto, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode switch
        {
            404 => NotFound(new { error = result.Error.Message }),
            409 => Conflict(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message })
        };
        return Ok(result.Value);
    }

    [Authorize(Policy = "StatusReport.View")]
    [HttpGet("{projectId:guid}/performance")]
    public async Task<IActionResult> GetProjectPerformance(
        Guid projectId,
        [FromQuery] DateTime? statusDate,
        [FromQuery] string perspective = "Consolidated",
        [FromQuery] Guid? planId = null,
        [FromQuery] Guid? comparePlanId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectReportingService.GetPerformanceAsync(projectId, statusDate, perspective, planId, comparePlanId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode == 404
            ? NotFound(new { error = result.Error.Message })
            : BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _projectService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.PublicId }, result.Value);
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

    [Authorize(Policy = "Handover.Initiate")]
    [HttpGet("{id:guid}/handover-managers")]
    public async Task<IActionResult> GetEligibleHandoverManagers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectHandoverRequestService.GetEligibleManagersAsync(id, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                401 => Unauthorized(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Capabilities")]
    [HttpGet("{id:guid}/handover-capabilities")]
    public async Task<IActionResult> GetHandoverCapabilities(Guid id, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _projectHandoverRequestService.GetCapabilitiesAsync(id, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : Forbid();
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

    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("{id:guid}/handover-requests/admin")]
    public async Task<IActionResult> CreateAdministrativeHandoverRequest(Guid id, [FromBody] CreateProjectHandoverRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _projectHandoverRequestService.CreateAdminInitiatedAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("{id:guid}/handover-requests/initial-assignment")]
    public async Task<IActionResult> CreateInitialManagerAssignment(Guid id, [FromBody] CreateProjectHandoverRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _projectHandoverRequestService.CreateInitialAssignmentAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                403 => Forbid(),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Handover.Access")]
    [HttpGet("{id:guid}/handover-requests")]
    public async Task<IActionResult> GetHandoverRequests(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _projectHandoverRequestService.GetForProjectAsync(id, userId, cancellationToken);
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
