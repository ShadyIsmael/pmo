using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.WorkflowEngine.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/workflows")]
[Authorize(Policy = "ProjectSettings.View")]
public sealed class ProjectWorkflowController : ControllerBase
{
    private readonly IProjectWorkflowConfigurationService _service;
    private readonly IManagementContextAccessor _managementContext;

    public ProjectWorkflowController(IProjectWorkflowConfigurationService service, IManagementContextAccessor managementContext)
    {
        _service = service;
        _managementContext = managementContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken) => Handle(await _service.GetAsync(projectId, cancellationToken));

    [HttpPut("{domain}")]
    [Authorize(Policy = "ProjectSettings.Edit")]
    public async Task<IActionResult> Save(Guid projectId, string domain, [FromBody] SaveProjectWorkflowConfigurationRequest request, CancellationToken cancellationToken)
    {
        var principal = User;
        var nameIdentifier = principal.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = nameIdentifier?.Value ?? (nameIdentifier is null ? principal.FindFirst("sub")?.Value : null);
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actorValue, out var actorId)
            || actorId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId == Guid.Empty)
            return Unauthorized();

        var context = _managementContext.Current;
        if (context is null || context.UserId != actorId || context.TenantId != tenantId || context.EffectiveReadOnly)
            return Forbid();

        return Handle(await _service.SaveAsync(projectId, domain, request, actorId.ToString("D"), cancellationToken));
    }

    private static IActionResult Handle<T>(NEXUS.SharedKernel.Result<T> result) => result.IsSuccess ? new OkObjectResult(result.Value) : Error(result.Error!);
    private static IActionResult Error(NEXUS.SharedKernel.Error error) => error.StatusCode switch
    {
        404 => new NotFoundObjectResult(new { error = error.Message }),
        403 => new ObjectResult(new { error = error.Message }) { StatusCode = 403 },
        409 => new ConflictObjectResult(new { error = error.Message }),
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}
