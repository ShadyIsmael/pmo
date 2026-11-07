using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/planning/calendar-templates")]
[Authorize]
public class PlanningCalendarController : ControllerBase
{
    private readonly IPlanningCalendarService _service;

    public PlanningCalendarController(IPlanningCalendarService service) => _service = service;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var result = await _service.GetTemplatesAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid templateId, CancellationToken cancellationToken)
    {
        var result = await _service.GetTemplateAsync(templateId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateCalendarTemplateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateTemplateAsync(dto, UserId(), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetTemplate), new { templateId = result.Value!.Id }, result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] UpdateCalendarTemplateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateTemplateAsync(templateId, dto, UserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid templateId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteTemplateAsync(templateId, rowVersion, UserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
