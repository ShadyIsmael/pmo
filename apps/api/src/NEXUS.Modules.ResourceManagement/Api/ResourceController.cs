using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resources")]
public class ResourceController : ControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly IValidator<CreateResourceDto> _createValidator;
    private readonly IValidator<UpdateResourceDto> _updateValidator;
    private readonly IManagementContextAccessor? _managementContext;

    public ResourceController(
        IResourceService resourceService,
        IValidator<CreateResourceDto> createValidator,
        IValidator<UpdateResourceDto> updateValidator,
        IManagementContextAccessor? managementContext = null)
    {
        _resourceService = resourceService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _managementContext = managementContext;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _resourceService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetPaged([FromQuery] ResourceFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _resourceService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("base-calendars")]
    [Authorize(Policy = "WorkflowLookup.Resource.BaseCalendars")]
    public async Task<IActionResult> GetBaseCalendars(CancellationToken cancellationToken)
    {
        return Ok(await _resourceService.GetBaseCalendarsAsync(cancellationToken));
    }

    [HttpGet("reference-data")]
    [Authorize(Policy = "WorkflowLookup.Resource.ReferenceData")]
    public async Task<IActionResult> GetReferenceData(CancellationToken cancellationToken)
    {
        return Ok(await _resourceService.GetReferenceDataAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Resource.Create")]
    public async Task<IActionResult> Create([FromBody] CreateResourceDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            var problem = new ValidationProblemDetails(validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct())
            };
            problem.Extensions["code"] = "VALIDATION_ERROR";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        var result = await _resourceService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return MapError(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Resource.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateResourceDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;

        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            var problem = new ValidationProblemDetails(validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct())
            };
            problem.Extensions["code"] = "VALIDATION_ERROR";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        var result = await _resourceService.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure) return MapError(result.Error!);
        return Ok(result.Value);
    }

    private IActionResult MapError(NEXUS.SharedKernel.Error error)
    {
        if (error.StatusCode == StatusCodes.Status404NotFound) return NotFound(new { error = error.Message });
        if (error.StatusCode == StatusCodes.Status409Conflict) return Conflict(new { error = error.Message });
        if (error.FieldName is not null)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [error.FieldName] = [error.Message]
            })
            {
                Status = error.StatusCode,
                Title = "Validation failed",
                Detail = error.Message
            };
            problem.Extensions["code"] = error.Code;
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return error.StatusCode == StatusCodes.Status400BadRequest
                ? BadRequest(problem)
                : new ObjectResult(problem) { StatusCode = error.StatusCode };
        }
        return BadRequest(new { error = error.Message });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Resource.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _resourceService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
