using System.Security.Claims;
using System.DirectoryServices.Protocols;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Infrastructure;

namespace NEXUS.Modules.Integrations.Api;

[ApiController]
[Route("api/directory-integrations")]
[Authorize]
public sealed class DirectoryIntegrationController(IDirectoryIntegrationService service) : ControllerBase
{
    [HttpGet("reference/organizational-units")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetOrganizationalUnits(CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetOrganizationalUnitsAsync(TenantId(), cancellationToken));

    [HttpGet("reference/calendars")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetCalendars(CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetCalendarsAsync(TenantId(), cancellationToken));
    [HttpGet("connections")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetConnectionsAsync(TenantId(), cancellationToken));

    [HttpPost("connections")]
    [Authorize(Policy = "Directory.Manage")]
    public async Task<IActionResult> CreateConnection([FromBody] SaveDirectoryConnectionRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveConnectionAsync(TenantId(), ActorId(), null, request, cancellationToken), StatusCodes.Status201Created);

    [HttpPut("connections/{connectionId:guid}")]
    [Authorize(Policy = "Directory.Manage")]
    public async Task<IActionResult> UpdateConnection(Guid connectionId, [FromBody] SaveDirectoryConnectionRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveConnectionAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpPost("connections/{connectionId:guid}/test")]
    [Authorize(Policy = "Directory.TestConnection")]
    public async Task<IActionResult> TestConnection(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.TestConnectionAsync(TenantId(), ActorId(), connectionId, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/scope")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetScope(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetScopeAsync(TenantId(), connectionId, cancellationToken));

    [HttpPut("connections/{connectionId:guid}/scope")]
    [Authorize(Policy = "Directory.Manage")]
    public async Task<IActionResult> SaveScope(Guid connectionId, [FromBody] SaveDirectorySyncScopeRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveScopeAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/field-mappings")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetFieldMappings(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetFieldMappingsAsync(TenantId(), connectionId, cancellationToken));

    [HttpPut("connections/{connectionId:guid}/field-mappings")]
    [Authorize(Policy = "Directory.ManageMappings")]
    public async Task<IActionResult> SaveFieldMappings(Guid connectionId, [FromBody] SaveDirectoryFieldMappingsRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveFieldMappingsAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/groups")]
    [Authorize(Policy = "Directory.GroupCatalog")]
    public async Task<IActionResult> GetGroups(Guid connectionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        await RunAsync(() => service.GetDirectoryGroupsAsync(TenantId(), connectionId, new DirectoryProviderQuery(page, pageSize, search), cancellationToken));

    [HttpGet("connections/{connectionId:guid}/group-mappings")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetGroupMappings(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetGroupMappingsAsync(TenantId(), connectionId, cancellationToken));

    [HttpPut("connections/{connectionId:guid}/group-mappings")]
    [Authorize(Policy = "Directory.ManageMappings")]
    public async Task<IActionResult> SaveGroupMappings(Guid connectionId, [FromBody] SaveDirectoryGroupMappingsRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveGroupMappingsAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/organization-mappings")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetOrganizationMappings(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetOrganizationMappingsAsync(TenantId(), connectionId, cancellationToken));

    [HttpPut("connections/{connectionId:guid}/organization-mappings")]
    [Authorize(Policy = "Directory.ManageMappings")]
    public async Task<IActionResult> SaveOrganizationMappings(Guid connectionId, [FromBody] SaveDirectoryOrganizationMappingsRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveOrganizationMappingsAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/provisioning-policy")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetProvisioningPolicy(Guid connectionId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetProvisioningPolicyAsync(TenantId(), connectionId, cancellationToken));

    [HttpPut("connections/{connectionId:guid}/provisioning-policy")]
    [Authorize(Policy = "Directory.ManageProvisioningPolicy")]
    public async Task<IActionResult> SaveProvisioningPolicy(Guid connectionId, [FromBody] SaveDirectoryProvisioningPolicyRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.SaveProvisioningPolicyAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("connections/{connectionId:guid}/preview/{itemType}")]
    [Authorize(Policy = "Directory.PreviewSync")]
    public async Task<IActionResult> Preview(Guid connectionId, string itemType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        await RunAsync(() => service.PreviewAsync(TenantId(), connectionId, itemType, new DirectoryProviderQuery(page, pageSize, search), cancellationToken));

    [HttpPost("connections/{connectionId:guid}/sync")]
    [Authorize(Policy = "Directory.ExecuteSync")]
    public async Task<IActionResult> ExecuteSync(Guid connectionId, [FromBody] ExecuteDirectorySyncRequest request, CancellationToken cancellationToken) =>
        await RunAsync(() => service.ExecuteSyncAsync(TenantId(), ActorId(), connectionId, request, cancellationToken));

    [HttpGet("sync-runs")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        await RunAsync(() => service.GetHistoryAsync(TenantId(), page, pageSize, cancellationToken));

    [HttpGet("sync-runs/{runId:guid}")]
    [Authorize(Policy = "Directory.View")]
    public async Task<IActionResult> GetHistoryDetail(Guid runId, CancellationToken cancellationToken) =>
        await RunAsync(() => service.GetHistoryDetailAsync(TenantId(), runId, cancellationToken));

    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action, int successStatus = StatusCodes.Status200OK)
    {
        try
        {
            return StatusCode(successStatus, await action());
        }
        catch (DirectoryIntegrationException exception)
        {
            return StatusCode(exception.StatusCode, new { error = exception.Message, code = exception.Code });
        }
        catch (DirectoryProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = exception.Message, code = exception.Code });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "The directory provider did not respond before the configured timeout.", code = "DIRECTORY_PROVIDER_TIMEOUT" });
        }
        catch (Exception exception) when (exception is LdapException or SocketException or MsalException or HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The directory provider could not complete the requested operation. Verify connectivity and provider configuration.", code = "DIRECTORY_PROVIDER_UNAVAILABLE" });
        }
    }

    private Guid TenantId() => Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId)
        ? tenantId
        : throw new UnauthorizedAccessException("The authenticated tenant is unavailable.");

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
        ? actorId
        : throw new UnauthorizedAccessException("The authenticated user is unavailable.");
}
