using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Api;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documentService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetPagedAsync(page, pageSize, projectId, cancellationToken);
        return Ok(result);
    }
}
