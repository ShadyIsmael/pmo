using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application.Settings;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class SettingsController : ControllerBase
{
    private readonly IConfigurationSettingsService _settings;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITenantFiscalYearService? _tenantFiscalYear;

    public SettingsController(
        IConfigurationSettingsService settings,
        IAuthorizationService authorizationService,
        ITenantFiscalYearService? tenantFiscalYear = null)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _tenantFiscalYear = tenantFiscalYear;
    }

    [HttpGet("access")]
    public async Task<ActionResult<SettingsAccessResponse>> GetAccess(CancellationToken cancellationToken)
    {
        var tenantRead = await _authorizationService.AuthorizeAsync(User, null, "Settings.View");
        var tenantWrite = await _authorizationService.AuthorizeAsync(User, null, "Settings.ManageTenantConfiguration");
        var platformRead = await _authorizationService.AuthorizeAsync(User, null, "PlatformSettings.Read");
        var platformWrite = await _authorizationService.AuthorizeAsync(User, null, "PlatformSettings.Write");
        return Ok(new SettingsAccessResponse(tenantRead.Succeeded, tenantWrite.Succeeded,
            platformRead.Succeeded, platformWrite.Succeeded));
    }

    [HttpGet("platform")]
    [Authorize(Policy = "PlatformSettings.Read")]
    public async Task<IActionResult> GetPlatform(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _settings.GetPlatformAsync(cancellationToken);
            return Ok(response);
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
    }

    [HttpPut("platform/{key}")]
    [Authorize(Policy = "PlatformSettings.Write")]
    public Task<IActionResult> UpdatePlatform(string key, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.UpdatePlatformAsync(key, request, cancellationToken));

    [HttpPost("platform/{key}/reset")]
    [Authorize(Policy = "PlatformSettings.Write")]
    public Task<IActionResult> ResetPlatform(string key, [FromBody] ResetSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.ResetPlatformAsync(key, request, cancellationToken));

    [HttpGet("tenant")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetTenant(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _settings.GetTenantAsync(cancellationToken);
            return ToActionResult(result);
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
    }

    [HttpGet("tenant/fiscal-year")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetTenantFiscalYearPreview([FromQuery] int? startMonth, CancellationToken cancellationToken)
    {
        if (startMonth is < 1 or > 12)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["startMonth"] = ["Fiscal year start month must be from 1 through 12."]
            }));

        try
        {
            var tenant = await _settings.GetTenantAsync(cancellationToken);
            if (!tenant.Succeeded || tenant.Value?.OwnerId is not { } tenantId)
                return ToActionResult(tenant);
            if (_tenantFiscalYear is null) return PersistenceUnavailable();
            return Ok(await _tenantFiscalYear.GetPreviewAsync(tenantId, startMonth, cancellationToken: cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
    }

    [HttpGet("organizations/{organizationId:guid}")]
    [Authorize(Policy = "Settings.View")]
    public async Task<IActionResult> GetOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            return ToActionResult(await _settings.GetOrganizationAsync(organizationId, cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
        catch (SettingsOwnerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("runtime")]
    public async Task<IActionResult> GetRuntime(CancellationToken cancellationToken)
    {
        try
        {
            return ToActionResult(await _settings.GetTenantRuntimeAsync(cancellationToken));
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
    }

    [HttpPut("tenant/{key}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public Task<IActionResult> UpdateTenant(string key, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.UpdateTenantAsync(key, request, cancellationToken));

    [HttpPost("tenant/{key}/reset")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public Task<IActionResult> ResetTenant(string key, [FromBody] ResetSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.ResetTenantAsync(key, request, cancellationToken));

    [HttpPut("organizations/{organizationId:guid}/{key}")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public Task<IActionResult> UpdateOrganization(
        Guid organizationId, string key, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.UpdateOrganizationAsync(organizationId, key, request, cancellationToken));

    [HttpPost("organizations/{organizationId:guid}/{key}/reset")]
    [Authorize(Policy = "Settings.ManageTenantConfiguration")]
    public Task<IActionResult> ResetOrganization(
        Guid organizationId, string key, [FromBody] ResetSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResultAsync(() => _settings.ResetOrganizationAsync(organizationId, key, request, cancellationToken));

    private async Task<IActionResult> ToActionResultAsync(Func<Task<SettingsOperationResult<EffectiveSettingDto>>> operation)
    {
        try
        {
            return ToActionResult(await operation());
        }
        catch (SettingsPersistenceUnavailableException)
        {
            return PersistenceUnavailable();
        }
        catch (SettingsOwnerNotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "Organization not found" });
        }
    }

    private IActionResult ToActionResult<T>(SettingsOperationResult<T> result)
    {
        if (result.Succeeded) return Ok(result.Value);
        return result.Code switch
        {
            SettingsOperationCode.UnknownSetting => NotFound(new ProblemDetails { Title = "Unknown setting", Detail = result.Error }),
            SettingsOperationCode.ScopeNotAllowed => BadRequest(new ProblemDetails { Title = "Setting scope is not allowed", Detail = result.Error }),
            SettingsOperationCode.ValidationFailed => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]> { ["value"] = new[] { result.Error ?? "Invalid value." } })),
            SettingsOperationCode.TenantContextMissing => Forbid(),
            SettingsOperationCode.AccessDenied => Forbid(),
            SettingsOperationCode.ConcurrencyConflict => Conflict(new ProblemDetails { Title = "Configuration changed", Detail = result.Error }),
            SettingsOperationCode.NotFound => NotFound(new ProblemDetails { Title = "Setting not found", Detail = result.Error }),
            SettingsOperationCode.PersistenceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Settings store unavailable", Detail = result.Error }),
            SettingsOperationCode.FiscalYearChangeBlocked => Conflict(new ProblemDetails
            {
                Title = "Fiscal year configuration change blocked",
                Detail = result.Error,
                Extensions = { ["code"] = "fiscalYearChangeBlocked" }
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult PersistenceUnavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails
        {
            Title = "Settings store unavailable",
            Detail = "The durable settings store is unavailable or its schema has not been deployed. Retry later."
        });
}
