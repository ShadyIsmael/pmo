using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;

namespace NEXUS.Modules.WorkflowEngine.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/workflows")]
[Authorize(Policy = "Project.View")]
public sealed class ProjectWorkflowController(IProjectWorkflowConfigurationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken) => Handle(await service.GetAsync(projectId, cancellationToken));

    [HttpPut("{domain}")]
    [Authorize(Policy = "Project.Edit")]
    public async Task<IActionResult> Save(Guid projectId, string domain, [FromBody] SaveProjectWorkflowConfigurationRequest request, CancellationToken cancellationToken)
    {
        var actor = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value ?? User.Identity?.Name ?? "System";
        return Handle(await service.SaveAsync(projectId, domain, request, actor, cancellationToken));
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
