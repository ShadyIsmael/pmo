using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Governance;

[ApiController]
[Authorize]
[Route("api/governance/assignment-recovery")]
public sealed class AssignmentRecoveryController : ControllerBase
{
    private readonly AssignmentRecoveryService _service;

    public AssignmentRecoveryController(AssignmentRecoveryService service) => _service = service;

    [HttpGet("{entityType}")]
    [Authorize(Policy = AssignmentRecoveryPermissions.View)]
    public Task<IActionResult> GetPage(
        string entityType, [FromQuery] string? search, [FromQuery] string? filter,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _service.GetPageAsync(TenantId(), ActorId(), entityType, search, filter, page, pageSize, cancellationToken));

    [HttpGet("projects/{publicId:guid}/summary")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Initiate)]
    [Authorize(Policy = "Handover.AdminInitiate")]
    public Task<IActionResult> GetInitialProjectManagerSummary(Guid publicId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetInitialProjectManagerSummaryAsync(TenantId(), ActorId(), publicId, cancellationToken));

    [HttpGet("{entityType}/{publicId:guid}/manager-candidates")]
    [Authorize(Policy = AssignmentRecoveryPermissions.View)]
    public Task<IActionResult> GetManagerCandidates(string entityType, Guid publicId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetManagerCandidatesAsync(TenantId(), ActorId(), entityType, publicId, cancellationToken));

    [HttpGet("projects/{publicId:guid}/sponsor-candidates")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Manage)]
    public Task<IActionResult> GetSponsorCandidates(CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetSponsorCandidatesAsync(TenantId(), ActorId(), cancellationToken));

    [HttpGet("{entityType}/{publicId:guid}/history")]
    [Authorize(Policy = AssignmentRecoveryPermissions.View)]
    public Task<IActionResult> GetHistory(string entityType, Guid publicId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetHistoryAsync(TenantId(), ActorId(), entityType, publicId, cancellationToken));

    [HttpPost("{entityType}/{publicId:guid}/initial-assignment")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Initiate)]
    [Authorize(Policy = "Handover.AdminInitiate")]
    public Task<IActionResult> CreateInitialAssignment(string entityType, Guid publicId, [FromBody] CreateRecoveryAssignmentDto input, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.InitiateInitialAssignmentAsync(TenantId(), ActorId(), entityType, publicId, input, cancellationToken), 201);

    [HttpPost("{entityType}/{publicId:guid}/administrative-handover")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Manage)]
    [Authorize(Policy = "Handover.AdminInitiate")]
    public Task<IActionResult> CreateAdministrativeHandover(string entityType, Guid publicId, [FromBody] CreateRecoveryAssignmentDto input, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.InitiateAdministrativeHandoverAsync(TenantId(), ActorId(), entityType, publicId, input, cancellationToken), 201);

    [HttpPost("projects/{publicId:guid}/sponsor")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Manage)]
    public Task<IActionResult> AssignProjectSponsor(Guid publicId, [FromBody] AssignProjectSponsorDto input, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.AssignProjectSponsorAsync(TenantId(), ActorId(), publicId, input.SponsorUserId, cancellationToken));

    [HttpPost("{entityType}/{publicId:guid}/reconcile")]
    [Authorize(Policy = AssignmentRecoveryPermissions.Manage)]
    public Task<IActionResult> Reconcile(string entityType, Guid publicId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.ReconcileAsync(TenantId(), ActorId(), entityType, publicId, cancellationToken));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new AssignmentRecoveryException(401, "The authenticated user identity is unavailable.");

    private Guid TenantId() => Guid.TryParse(User.FindFirstValue("tenantId"), out var id)
        ? id : throw new AssignmentRecoveryException(401, "The authenticated tenant context is unavailable.");

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, int successStatusCode = 200)
    {
        try
        {
            var result = await action();
            return StatusCode(successStatusCode, result);
        }
        catch (AssignmentRecoveryException exception)
        {
            return StatusCode(exception.Error.StatusCode, exception.Error);
        }
        catch (DbUpdateException)
        {
            return StatusCode(StatusCodes.Status409Conflict, new Error(
                "ASSIGNMENT_RECOVERY_CONCURRENT_CONFLICT",
                "A concurrent governance update was saved. Refresh the project and try again.",
                StatusCodes.Status409Conflict));
        }
    }
}
