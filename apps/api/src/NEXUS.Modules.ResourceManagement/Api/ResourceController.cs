using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resources")]
public class ResourceController : ControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly IValidator<CreateResourceDto> _createValidator;
    private readonly IValidator<UpdateResourceDto> _updateValidator;

    public ResourceController(
        IResourceService resourceService,
        IValidator<CreateResourceDto> createValidator,
        IValidator<UpdateResourceDto> updateValidator)
    {
        _resourceService = resourceService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetBaseCalendars(CancellationToken cancellationToken)
    {
        return Ok(await _resourceService.GetBaseCalendarsAsync(cancellationToken));
    }

    [HttpGet("reference-data")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetReferenceData(CancellationToken cancellationToken)
    {
        return Ok(await _resourceService.GetReferenceDataAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Resource.Create")]
    public async Task<IActionResult> Create([FromBody] CreateResourceDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest
            });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _resourceService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return MapError(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Resource.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateResourceDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest
            });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
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
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [error.FieldName] = [error.Message]
            })
            {
                Status = StatusCodes.Status400BadRequest
            });
        }
        return BadRequest(new { error = error.Message });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Resource.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _resourceService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
