using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/currencies")]
[Authorize]
public sealed class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet]
    [Authorize(Policy = "ReferenceData.View")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _currencyService.GetAllAsync(cancellationToken));
    }

    [HttpGet("project-lookup")]
    [Authorize(Policy = "WorkflowLookup.Project.Currencies")]
    public async Task<IActionResult> GetProjectLookup(CancellationToken cancellationToken) =>
        Ok(await _currencyService.GetProjectLookupAsync(cancellationToken));

    [HttpGet("workflow-lookup")]
    [Authorize(Policy = "WorkflowLookup.Business.Currencies")]
    public async Task<IActionResult> GetWorkflowLookup(CancellationToken cancellationToken) =>
        Ok(await _currencyService.GetProjectLookupAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "ReferenceData.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _currencyService.GetByIdAsync(id, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "ReferenceData.Create")]
    public async Task<IActionResult> Create([FromBody] SaveCurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _currencyService.CreateAsync(request, actorId, cancellationToken);
        return result.IsFailure
            ? StatusCode(result.Error!.StatusCode, result.Error)
            : CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ReferenceData.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _currencyService.UpdateAsync(id, request, actorId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ReferenceData.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _currencyService.DeleteAsync(id, actorId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : NoContent();
    }

    private bool TryGetActorId(out string actorId)
    {
        actorId = string.Empty;
        var hasActorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActorId)
            && parsedActorId != Guid.Empty;
        if (!hasActorId)
            hasActorId = Guid.TryParse(User.FindFirstValue("sub"), out parsedActorId) && parsedActorId != Guid.Empty;
        if (!hasActorId) return false;
        actorId = parsedActorId.ToString("D");
        return true;
    }
}
