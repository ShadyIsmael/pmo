using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Documents.Application;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Documents.Api;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly IManagementContextAccessor _managementContext;

    public DocumentController(IDocumentService documents, IManagementContextAccessor managementContext)
    {
        _documents = documents;
        _managementContext = managementContext;
    }

    [HttpGet]
    [Authorize(Policy = DocumentPermissions.View)]
    public async Task<IActionResult> GetPaged([FromQuery] DocumentListQuery query, CancellationToken cancellationToken)
    {
        if (query.ProjectId.HasValue && _managementContext.Current is { } context && !context.Scope.ProjectIds.Contains(query.ProjectId.Value))
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not Found", Detail = "The requested project was not found." });

        return Ok(await _documents.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("categories")]
    [Authorize(Policy = DocumentPermissions.View)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
        => Ok((await _documents.GetCategoriesAsync(cancellationToken)).Value);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DocumentPermissions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.GetByIdAsync(id, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 404, result.Error) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = DocumentPermissions.Create)]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] DocumentMutationDto dto, CancellationToken cancellationToken)
    {
        var result = await _documents.CreateAsync(dto, CurrentUserId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 400, result.Error) : CreatedAtAction(nameof(GetById), new { id = result.Value!.Document.Id }, result.Value);
    }

    [HttpPost("{id:guid}/versions")]
    [Authorize(Policy = DocumentPermissions.Edit)]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> AddVersion(Guid id, IFormFile file, [FromForm] string? changeNotes, CancellationToken cancellationToken)
    {
        var result = await _documents.AddVersionAsync(id, file, changeNotes, CurrentUserId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 400, result.Error) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = DocumentPermissions.Archive)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.ArchiveAsync(id, CurrentUserId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error?.StatusCode ?? 400, result.Error) : NoContent();
    }

    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = DocumentPermissions.View)]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? version, CancellationToken cancellationToken)
    {
        var result = await _documents.GetDownloadAsync(id, version, cancellationToken);
        if (result.IsFailure) return StatusCode(result.Error?.StatusCode ?? 404, result.Error);
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/preview")]
    [Authorize(Policy = DocumentPermissions.View)]
    public async Task<IActionResult> Preview(Guid id, [FromQuery] int? version, CancellationToken cancellationToken)
    {
        var result = await _documents.GetDownloadAsync(id, version, cancellationToken);
        if (result.IsFailure) return StatusCode(result.Error?.StatusCode ?? 404, result.Error);
        Response.Headers.ContentDisposition = $"inline; filename=\"{Uri.EscapeDataString(result.Value!.FileName)}\"";
        return File(result.Value.Content, result.Value.ContentType);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
