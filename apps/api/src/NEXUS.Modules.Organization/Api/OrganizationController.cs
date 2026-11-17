using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using System.Security.Claims;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationSettingsAccessService _settingsAccess;
    private readonly IAuthorizationService? _authorizationService;

    public OrganizationController(
        IOrganizationService organizationService,
        IOrganizationSettingsAccessService settingsAccess,
        IAuthorizationService? authorizationService = null)
    {
        _organizationService = organizationService;
        _settingsAccess = settingsAccess;
        _authorizationService = authorizationService;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _organizationService.GetPagedDirectoryAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("general-settings")]
    public async Task<ActionResult<IReadOnlyList<OrganizationProfileDto>>> GetGeneralSettings(CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForSettingsAsync("Settings.View")
            && !await IsAuthorizedForSettingsAsync("OrganizationConfiguration.View"))
            return Forbid();
        if (!Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId) || tenantId == Guid.Empty) return Forbid();
        try
        {
            var result = await _organizationService.GetProfilesAsync(cancellationToken);
            return Ok(result);
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings unavailable", Detail = "Organization settings access storage is unavailable or its schema has not been deployed." });
        }
    }

    [HttpPut("{id:guid}/general-settings")]
    public async Task<IActionResult> UpdateGeneralSettings(
        Guid id,
        [FromBody] UpdateOrganizationProfileRequest request,
        CancellationToken cancellationToken)
    {
        var hasTenantConfigurationAuthority = await IsAuthorizedForSettingsAsync("Settings.ManageTenantConfiguration");
        var hasOrganizationConfigurationAuthority = !hasTenantConfigurationAuthority
            && await IsAuthorizedForSettingsAsync("OrganizationConfiguration.Manage")
            && await _settingsAccess.HasScopedOrganizationAdminAuthorityAsync(id, manage: true, cancellationToken);
        if (!hasTenantConfigurationAuthority && !hasOrganizationConfigurationAuthority) return Forbid();

        OrganizationProfileUpdateResult result;
        try
        {
            result = await _organizationService.UpdateProfileAsync(id, request, cancellationToken);
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings unavailable", Detail = "Organization settings access storage is unavailable or its schema has not been deployed." });
        }
        return result.Code switch
        {
            OrganizationProfileUpdateCode.Updated => Ok(result.Profile),
            OrganizationProfileUpdateCode.NotFound => NotFound(),
            OrganizationProfileUpdateCode.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Title = "Organization changed",
                Detail = result.Error
            }),
            OrganizationProfileUpdateCode.TenantContextMissing => Forbid(),
            OrganizationProfileUpdateCode.AccessDenied => Forbid(),
            OrganizationProfileUpdateCode.AuditFailed => StatusCode(StatusCodes.Status500InternalServerError,
                new ProblemDetails { Title = "Audit trail unavailable", Detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:guid}/settings-access/grants")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> GetSettingsGrants(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settingsAccess.GetGrantsAsync(id, cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings access unavailable" });
        }
    }

    [HttpGet("settings-access/users")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> GetSettingsGrantTargets(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settingsAccess.GetGrantTargetsAsync(cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings access unavailable" });
        }
    }

    [HttpPut("{id:guid}/settings-access/grants")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> SaveSettingsGrant(
        Guid id, [FromBody] UpdateOrganizationSettingsGrantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ToGrantActionResult(await _settingsAccess.SaveGrantAsync(id, request, cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings access unavailable" });
        }
    }

    [HttpPost("{id:guid}/settings-access/grants/{userId:guid}/revoke")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public async Task<IActionResult> RevokeSettingsGrant(
        Guid id, Guid userId, [FromBody] RevokeOrganizationSettingsGrantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ToGrantActionResult(await _settingsAccess.RevokeGrantAsync(id, userId, request, cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Organization settings access unavailable" });
        }
    }

    private IActionResult ToGrantActionResult(OrganizationSettingsGrantOperationResult result) => result.Code switch
    {
        OrganizationSettingsGrantOperationCode.Saved or OrganizationSettingsGrantOperationCode.Revoked => Ok(result.Grant),
        OrganizationSettingsGrantOperationCode.NotFound => NotFound(),
        OrganizationSettingsGrantOperationCode.TenantContextMissing => Forbid(),
        OrganizationSettingsGrantOperationCode.ConcurrencyConflict => Conflict(new ProblemDetails
        {
            Title = "Organization settings access changed",
            Detail = "Reload grants and retry the change."
        }),
        OrganizationSettingsGrantOperationCode.ValidationFailed => BadRequest(new ProblemDetails
        {
            Title = "Invalid organization settings grant",
            Detail = result.Error
        }),
        OrganizationSettingsGrantOperationCode.PersistenceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new ProblemDetails { Title = "Organization settings audit unavailable", Detail = result.Error }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private async Task<bool> IsAuthorizedForSettingsAsync(string policy)
    {
        if (_authorizationService is null || User.Identity?.IsAuthenticated != true) return false;
        var result = await _authorizationService.AuthorizeAsync(User, resource: null, policyName: policy);
        return result.Succeeded;
    }
}
