using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/organizations/{organizationId:guid}/phase-gate-requirements")]
[Authorize]
public sealed class PhaseGateRequirementsController : ControllerBase
{
    private readonly IPhaseGateConfigurationService _service;

    public PhaseGateRequirementsController(IPhaseGateConfigurationService service) => _service = service;

    [HttpGet("transitions")]
    public async Task<IActionResult> GetTransitions(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = await _service.GetPhasePairsAsync(organizationId, cancellationToken);
        return result.Code switch
        {
            PhaseGateConfigurationOperationCode.Success => Ok(result.PhasePairs),
            PhaseGateConfigurationOperationCode.TenantContextMissing or PhaseGateConfigurationOperationCode.AccessDenied => Forbid(),
            PhaseGateConfigurationOperationCode.PersistenceUnavailable => PersistenceUnavailable(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetConfiguration(
        Guid organizationId,
        [FromQuery] Guid? sourcePhaseId,
        [FromQuery] Guid? targetPhaseId,
        CancellationToken cancellationToken)
    {
        if (!sourcePhaseId.HasValue || sourcePhaseId == Guid.Empty || !targetPhaseId.HasValue || targetPhaseId == Guid.Empty)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["phasePair"] = ["Distinct source and target phase IDs are required."]
            }));
        return ToActionResult(await _service.GetConfigurationAsync(organizationId, sourcePhaseId.Value, targetPhaseId.Value, cancellationToken));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        Guid organizationId,
        [FromQuery] Guid? sourcePhaseId,
        [FromQuery] Guid? targetPhaseId,
        CancellationToken cancellationToken)
    {
        if (!sourcePhaseId.HasValue || sourcePhaseId == Guid.Empty || !targetPhaseId.HasValue || targetPhaseId == Guid.Empty)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["phasePair"] = ["Distinct source and target phase IDs are required."]
            }));
        return ToActionResult(await _service.GetHistoryAsync(organizationId, sourcePhaseId.Value, targetPhaseId.Value, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> SaveConfiguration(
        Guid organizationId,
        [FromQuery] Guid? sourcePhaseId,
        [FromQuery] Guid? targetPhaseId,
        [FromBody] SavePhaseGateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!sourcePhaseId.HasValue || sourcePhaseId == Guid.Empty || !targetPhaseId.HasValue || targetPhaseId == Guid.Empty)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["phasePair"] = ["Distinct source and target phase IDs are required."]
            }));
        return ToActionResult(await _service.SaveConfigurationAsync(
            organizationId, sourcePhaseId.Value, targetPhaseId.Value, request, cancellationToken));
    }

    [HttpGet("templates-by-category")]
    public async Task<IActionResult> GetTemplatesByCategory(
        Guid organizationId,
        [FromQuery] Guid? sourcePhaseId,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!sourcePhaseId.HasValue || sourcePhaseId == Guid.Empty)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sourcePhaseId"] = ["A source phase ID is required."]
            }));
        return ToActionResult(await _service.GetTemplatesByCategoryAsync(organizationId, sourcePhaseId.Value, categoryId, cancellationToken));
    }

    private IActionResult ToActionResult(PhaseGateConfigurationOperationResult result) => result.Code switch
    {
        PhaseGateConfigurationOperationCode.Success => Ok((object?)result.Configuration ?? result.History),
        PhaseGateConfigurationOperationCode.NotFound => NotFound(),
        PhaseGateConfigurationOperationCode.TenantContextMissing or PhaseGateConfigurationOperationCode.AccessDenied => Forbid(),
        PhaseGateConfigurationOperationCode.ValidationFailed => BadRequest(new ValidationProblemDetails(
            result.ValidationErrors?.ToDictionary(item => item.Key, item => item.Value)
            ?? new Dictionary<string, string[]> { ["configuration"] = [result.Error ?? "The configuration is invalid."] })),
        PhaseGateConfigurationOperationCode.ConcurrencyConflict => Conflict(new ProblemDetails
        {
            Title = "Phase-gate configuration changed",
            Detail = result.Error ?? "Reload the current configuration before saving."
        }),
        PhaseGateConfigurationOperationCode.AuditFailed or PhaseGateConfigurationOperationCode.PersistenceUnavailable =>
            PersistenceUnavailable(result.Error),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private IActionResult PersistenceUnavailable(string? detail = null) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Title = "Phase-gate configuration unavailable",
            Detail = detail ?? "The configuration store or audit trail is unavailable, or its schema has not been deployed."
        });
}
