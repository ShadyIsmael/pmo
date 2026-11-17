using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Api;

[ApiController]
[Route("api/workflows")]
[Authorize(Policy = "Workflow.View")]
public sealed class WorkflowManagementController : ControllerBase
{
    private readonly IWorkflowManagementService _service;

    public WorkflowManagementController(IWorkflowManagementService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] string? entityType, [FromQuery] string? domain, CancellationToken cancellationToken) => Handle(await _service.ListDefinitionsAsync(search, isActive, entityType, domain, cancellationToken));

    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata(CancellationToken cancellationToken) => Handle(await _service.GetMetadataAsync(cancellationToken));

    [HttpGet("{definitionId:guid}")]
    public async Task<IActionResult> Definition(Guid definitionId, CancellationToken cancellationToken) => Handle(await _service.GetDefinitionAsync(definitionId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateDefinitionAsync(request, Actor(), cancellationToken));

    [HttpPatch("{definitionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> Update(Guid definitionId, [FromBody] UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken) => Handle(await _service.UpdateDefinitionAsync(definitionId, request, Actor(), cancellationToken));

    [HttpPost("{definitionId:guid}/drafts")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> CreateDraft(Guid definitionId, [FromBody] CreateDraftRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateDraftAsync(definitionId, request, Actor(), cancellationToken));

    [HttpGet("versions/{versionId:guid}")]
    public async Task<IActionResult> Version(Guid versionId, CancellationToken cancellationToken) => Handle(await _service.GetVersionAsync(versionId, cancellationToken));

    [HttpDelete("versions/{versionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> DeleteDraft(Guid versionId, [FromBody] DeleteVersionRequest request, CancellationToken cancellationToken) => Handle(await _service.DeleteDraftAsync(versionId, request.RowVersion, Actor(), cancellationToken));

    [HttpPost("versions/{versionId:guid}/validate")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> Validate(Guid versionId, CancellationToken cancellationToken) => Handle(await _service.ValidateAsync(versionId, cancellationToken));

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = "Workflow.Publish")]
    public async Task<IActionResult> Publish(Guid versionId, [FromBody] PublishWorkflowRequest request, CancellationToken cancellationToken) => Handle(await _service.PublishAsync(versionId, request, Actor(), cancellationToken));

    [HttpGet("versions/{versionId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid versionId, CancellationToken cancellationToken) => Handle(await _service.PreviewAsync(versionId, cancellationToken));

    [HttpPost("versions/{versionId:guid}/states")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> CreateState(Guid versionId, [FromBody] SaveWorkflowStateRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateStateAsync(versionId, request, Actor(), cancellationToken));

    [HttpPatch("states/{stateId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> UpdateState(Guid stateId, [FromBody] SaveWorkflowStateRequest request, CancellationToken cancellationToken) => Handle(await _service.UpdateStateAsync(stateId, request, Actor(), cancellationToken));

    [HttpDelete("states/{stateId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> DeleteState(Guid stateId, [FromBody] DeleteVersionRequest request, CancellationToken cancellationToken) => Handle(await _service.DeleteStateAsync(stateId, request.RowVersion, Actor(), cancellationToken));

    [HttpPost("versions/{versionId:guid}/transitions")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> CreateTransition(Guid versionId, [FromBody] SaveWorkflowTransitionRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateTransitionAsync(versionId, request, Actor(), cancellationToken));

    [HttpPatch("transitions/{transitionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> UpdateTransition(Guid transitionId, [FromBody] SaveWorkflowTransitionRequest request, CancellationToken cancellationToken) => Handle(await _service.UpdateTransitionAsync(transitionId, request, Actor(), cancellationToken));

    [HttpDelete("transitions/{transitionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> DeleteTransition(Guid transitionId, [FromBody] DeleteVersionRequest request, CancellationToken cancellationToken) => Handle(await _service.DeleteTransitionAsync(transitionId, request.RowVersion, Actor(), cancellationToken));

    [HttpPost("transitions/{transitionId:guid}/guards")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> CreateGuard(Guid transitionId, [FromBody] SaveWorkflowGuardRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateGuardAsync(transitionId, request, Actor(), cancellationToken));

    [HttpPatch("guards/{guardId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> UpdateGuard(Guid guardId, [FromBody] SaveWorkflowGuardRequest request, CancellationToken cancellationToken) => Handle(await _service.UpdateGuardAsync(guardId, request, Actor(), cancellationToken));

    [HttpDelete("guards/{guardId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> DeleteGuard(Guid guardId, [FromBody] DeleteVersionRequest request, CancellationToken cancellationToken) => Handle(await _service.DeleteGuardAsync(guardId, request.RowVersion, Actor(), cancellationToken));

    [HttpPost("transitions/{transitionId:guid}/actions")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> CreateAction(Guid transitionId, [FromBody] SaveWorkflowActionRequest request, CancellationToken cancellationToken) => Handle(await _service.CreateActionAsync(transitionId, request, Actor(), cancellationToken));

    [HttpPatch("actions/{actionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> UpdateAction(Guid actionId, [FromBody] SaveWorkflowActionRequest request, CancellationToken cancellationToken) => Handle(await _service.UpdateActionAsync(actionId, request, Actor(), cancellationToken));

    [HttpDelete("actions/{actionId:guid}")]
    [Authorize(Policy = "Workflow.Manage")]
    public async Task<IActionResult> DeleteAction(Guid actionId, [FromBody] DeleteVersionRequest request, CancellationToken cancellationToken) => Handle(await _service.DeleteActionAsync(actionId, request.RowVersion, Actor(), cancellationToken));

    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("userId") ?? string.Empty;

    private static IActionResult Handle<T>(Result<T> result) => result.IsSuccess ? new OkObjectResult(result.Value) : ErrorResult(result.Error!);
    private static IActionResult Handle(Result result) => result.IsSuccess ? new NoContentResult() : ErrorResult(result.Error!);
    private static IActionResult ErrorResult(Error error) => error.StatusCode switch
    {
        401 => new UnauthorizedObjectResult(new { code = error.Code, error = error.Message }),
        404 => new NotFoundObjectResult(new { code = error.Code, error = error.Message }),
        403 => new ObjectResult(new { code = error.Code, error = error.Message }) { StatusCode = 403 },
        409 => new ConflictObjectResult(new { code = error.Code, error = error.Message }),
        _ => new BadRequestObjectResult(new { code = error.Code, error = error.Message })
    };
}

public sealed class DeleteVersionRequest
{
    public byte[]? RowVersion { get; set; }
}
