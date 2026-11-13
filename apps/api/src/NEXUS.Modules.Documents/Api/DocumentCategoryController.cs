using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Api;

[ApiController]
[Route("api/document-categories")]
[Authorize]
public sealed class DocumentCategoryController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentCategoryController(IDocumentService documents) => _documents = documents;

    [HttpGet]
    [Authorize(Policy = DocumentCategoryPermissions.View)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid organizationId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _documents.GetManagedCategoriesAsync(organizationId, search, includeInactive, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 403, result.Error) : Ok(result.Value);
    }

    [HttpGet("template-options")]
    [Authorize(Policy = DocumentCategoryPermissions.TemplateOptionsLookupPolicy)]
    public async Task<IActionResult> GetTemplateOptions(
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var result = await _documents.GetTemplateCategoryOptionsAsync(organizationId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 403, result.Error) : Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DocumentCategoryPermissions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.GetManagedCategoryAsync(id, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 404, result.Error) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = DocumentCategoryPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] DocumentCategoryCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _documents.CreateCategoryAsync(request, CurrentUserId(), cancellationToken);
        return result.IsFailure
            ? StatusCode(result.Error?.StatusCode ?? 400, result.Error)
            : CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = DocumentCategoryPermissions.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentCategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _documents.UpdateCategoryAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 400, result.Error) : Ok(result.Value);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = DocumentCategoryPermissions.ActivateDeactivate)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] DocumentCategoryStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _documents.SetCategoryStatusAsync(id, request.IsActive, CurrentUserId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 400, result.Error) : Ok(result.Value);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
