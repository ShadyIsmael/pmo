using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/vendors")]
public class VendorController : ControllerBase
{
    private readonly IVendorService _vendorService;
    private readonly IManagementContextAccessor? _managementContext;

    public VendorController(IVendorService vendorService, IManagementContextAccessor? managementContext = null)
    {
        _vendorService = vendorService;
        _managementContext = managementContext;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _vendorService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var result = await _vendorService.GetPagedAsync(page, pageSize, search, categoryId, isActive, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns minimal active Vendor options for Resource forms.</summary>
    [HttpGet("workflow-lookups/resource-options")]
    [Authorize(Policy = "WorkflowLookup.Resource.Vendors")]
    public async Task<IActionResult> GetResourceVendorOptions([FromQuery] Guid? resourceId, CancellationToken cancellationToken)
    {
        return Ok(await _vendorService.GetResourceVendorOptionsAsync(resourceId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Resource.Create")]
    public async Task<IActionResult> Create([FromBody] CreateVendorDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _vendorService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Resource.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVendorDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _vendorService.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(),
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "Resource.Delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _vendorService.DeactivateAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(),
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/reactivate")]
    [Authorize(Policy = "Resource.Delete")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _vendorService.ReactivateAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(),
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Resource.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _vendorService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                401 => Unauthorized(),
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return NoContent();
    }

}
