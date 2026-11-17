using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Api;

[ApiController]
[Route("api/work-items")]
[Authorize]
public class WorkItemController : ControllerBase
{
    private readonly IWorkItemService _workItemService;
    private readonly IWorkItemWorkflowProvisioningService _workflowProvisioning;

    public WorkItemController(IWorkItemService workItemService, IWorkItemWorkflowProvisioningService workflowProvisioning)
    {
        _workItemService = workItemService;
        _workflowProvisioning = workflowProvisioning;
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workItemService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{projectId:guid}/assignees")]
    public async Task<IActionResult> GetAssignees(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _workItemService.GetAssigneesAsync(projectId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _workItemService.GetActivityAsync(id, page, pageSize, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _workItemService.GetCommentsAsync(id, page, pageSize, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.Edit")]
    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateWorkItemCommentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.AddCommentAsync(id, dto, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workItemService.GetAttachmentsAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.Edit")]
    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddAttachments(Guid id, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.AddAttachmentsAsync(id, files, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> GetAttachmentContent(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _workItemService.GetAttachmentContentAsync(id, attachmentId, cancellationToken);
        if (result.IsFailure) return ToActionResult(result.Error);
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    [Authorize(Policy = "WorkItem.Edit")]
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.DeleteAttachmentAsync(id, attachmentId, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights([FromQuery] Guid? projectId = null, [FromQuery] string? status = null, [FromQuery] string? type = null, [FromQuery] string? priority = null, [FromQuery] string? search = null, [FromQuery] Guid? assignedResourceId = null, [FromQuery] Guid? assignedTeamId = null, [FromQuery] Guid? assigneeId = null, [FromQuery] Guid? reporterId = null, [FromQuery] DateTime? dueDateFrom = null, [FromQuery] DateTime? dueDateTo = null, [FromQuery] bool? overdue = null, [FromQuery] bool? unassigned = null, [FromQuery] bool? highPriority = null, CancellationToken cancellationToken = default)
    {
        return Ok(await _workItemService.GetInsightsAsync(projectId, status, type, priority, search, assignedResourceId, assignedTeamId, assigneeId, reporterId, dueDateFrom, dueDateTo, overdue, unassigned, highPriority, cancellationToken));
    }

    [Authorize(Policy = "WorkItem.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] string? status = null, [FromQuery] string? type = null, [FromQuery] string? priority = null, [FromQuery] string? search = null, [FromQuery] Guid? assignedResourceId = null, [FromQuery] Guid? assignedTeamId = null, [FromQuery] Guid? assigneeId = null, [FromQuery] Guid? reporterId = null, [FromQuery] DateTime? dueDateFrom = null, [FromQuery] DateTime? dueDateTo = null, [FromQuery] bool? overdue = null, [FromQuery] bool? unassigned = null, [FromQuery] bool? highPriority = null, [FromQuery] bool myWork = false, [FromQuery] string? sortBy = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.GetPagedAsync(page, pageSize, projectId, status, type, priority, search, assignedResourceId, assignedTeamId, assigneeId, reporterId, dueDateFrom, dueDateTo, overdue, unassigned, highPriority, myWork, actorId, sortBy, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "WorkItem.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkItemDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.CreateAsync(dto, actorId, cancellationToken);
        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "WorkItem.Edit")]
    [HttpPost("workflow/provision")]
    public async Task<IActionResult> ProvisionWorkflow(CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workflowProvisioning.OnboardCurrentTenantAsync(actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToActionResult(result.Error);
    }

    [Authorize(Policy = "WorkItem.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkItemDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await _workItemService.UpdateAsync(id, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "WorkItem.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _workItemService.DeleteAsync(id, actorId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }

    private bool TryGetActorId(out string actorId)
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) && parsed != Guid.Empty)
        {
            actorId = parsed.ToString();
            return true;
        }

        actorId = string.Empty;
        return false;
    }

    private IActionResult ToActionResult(Result<WorkItemDto> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error?.StatusCode switch
        {
            404 => NotFound(new { error = result.Error.Message }),
            409 => Conflict(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message })
        };
    }

    private IActionResult ToActionResult(Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        401 => Unauthorized(new { error = error.Message }),
        403 => StatusCode(StatusCodes.Status403Forbidden, new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
