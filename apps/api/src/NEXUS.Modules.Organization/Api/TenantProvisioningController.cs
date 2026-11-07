using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/platform/tenants")]
[Authorize(Policy = "PlatformTenants.Manage")]
public sealed class TenantProvisioningController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioning;

    public TenantProvisioningController(ITenantProvisioningService provisioning) => _provisioning = provisioning;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _provisioning.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _provisioning.CreateAsync(request, cancellationToken);
        return result.Code switch
        {
            TenantProvisioningCode.Created => StatusCode(StatusCodes.Status201Created, result.Value),
            TenantProvisioningCode.DuplicateCode => Conflict(new ProblemDetails { Title = "Tenant code already exists", Detail = result.Error }),
            TenantProvisioningCode.ValidationFailed => BadRequest(new ProblemDetails { Title = "Invalid tenant provisioning request", Detail = result.Error }),
            TenantProvisioningCode.AdministratorNotFound or TenantProvisioningCode.AdministratorRoleUnavailable =>
                BadRequest(new ProblemDetails { Title = "Initial administrator could not be assigned", Detail = result.Error }),
            TenantProvisioningCode.AuditUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Tenant audit unavailable", Detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{tenantId:guid}/administrators/{userId:guid}")]
    public async Task<IActionResult> AssignAdministrator(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await _provisioning.AssignAdministratorAsync(tenantId, userId, cancellationToken);
        return result.Code switch
        {
            TenantProvisioningCode.StatusChanged => Ok(result.Value),
            TenantProvisioningCode.TenantNotFound or TenantProvisioningCode.AdministratorNotFound => NotFound(new ProblemDetails
            {
                Title = "Tenant or administrator not found",
                Detail = result.Error
            }),
            TenantProvisioningCode.AdministratorRoleUnavailable or TenantProvisioningCode.ValidationFailed =>
                BadRequest(new ProblemDetails { Title = "Administrator assignment failed", Detail = result.Error }),
            TenantProvisioningCode.ConcurrencyConflict => Conflict(new ProblemDetails { Title = "Tenant changed", Detail = result.Error }),
            TenantProvisioningCode.AuditUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Tenant audit unavailable", Detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{tenantId:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken) =>
        ChangeLifecycle(tenantId, NEXUS.Modules.Organization.Domain.TenantLifecycleStatus.Suspended, cancellationToken);

    [HttpPost("{tenantId:guid}/retire")]
    public Task<IActionResult> Retire(Guid tenantId, CancellationToken cancellationToken) =>
        ChangeLifecycle(tenantId, NEXUS.Modules.Organization.Domain.TenantLifecycleStatus.Retired, cancellationToken);

    private async Task<IActionResult> ChangeLifecycle(
        Guid tenantId,
        NEXUS.Modules.Organization.Domain.TenantLifecycleStatus status,
        CancellationToken cancellationToken)
    {
        var result = await _provisioning.ChangeLifecycleAsync(tenantId, status, cancellationToken);
        return result.Code switch
        {
            TenantProvisioningCode.Activated => Ok(result.Value),
            TenantProvisioningCode.TenantNotFound => NotFound(),
            TenantProvisioningCode.InvalidLifecycleTransition => Conflict(new ProblemDetails
            {
                Title = "Invalid tenant lifecycle transition",
                Detail = result.Error
            }),
            TenantProvisioningCode.ConcurrencyConflict => Conflict(new ProblemDetails { Title = "Tenant changed", Detail = result.Error }),
            TenantProvisioningCode.AuditUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Tenant audit unavailable", Detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
