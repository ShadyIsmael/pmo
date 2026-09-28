using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/reference-data/vendor-categories")]
public class VendorCategoryController : ControllerBase
{
    private readonly IVendorCategoryService _service;

    public VendorCategoryController(IVendorCategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "VendorCategory.View")]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var result = activeOnly
            ? await _service.GetActiveAsync(cancellationToken)
            : await _service.GetAllAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "VendorCategory.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "VendorCategory.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateLookupDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "VendorCategory.Manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLookupDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "VendorCategory.Manage")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.DeactivateAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
