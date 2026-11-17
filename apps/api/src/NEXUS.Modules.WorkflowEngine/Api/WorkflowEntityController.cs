using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.WorkflowEngine.Api;

[ApiController]
[Route("api/workflow/entities")]
[Authorize]
public class WorkflowEntityController : ControllerBase
{
    private readonly IWorkflowInstanceService _instanceService;
    private readonly IWorkflowTransitionQueryService _transitionQueryService;
    private readonly IWorkflowExecutionService _executionService;
    private readonly DbContext _context;

    public WorkflowEntityController(
        IWorkflowInstanceService instanceService,
        IWorkflowTransitionQueryService transitionQueryService,
        IWorkflowExecutionService executionService,
        DbContext context)
    {
        _instanceService = instanceService;
        _transitionQueryService = transitionQueryService;
        _executionService = executionService;
        _context = context;
    }

    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<IActionResult> GetInstance(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var ctx = BuildExecutionContext();
        var identityError = ValidateExecutionIdentity(ctx);
        if (identityError is not null) return identityError;
        var result = await _instanceService.GetInstanceAsync(entityType, entityId, ctx, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleError(result.Error!);
    }

    [HttpPost("{entityType}/{entityId:guid}/provision")]
    public async Task<IActionResult> ProvisionInstance(
        string entityType,
        Guid entityId,
        [FromBody] ProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = BuildExecutionContext();
        var identityError = ValidateExecutionIdentity(ctx);
        if (identityError is not null) return identityError;
        var result = await _instanceService.ProvisionAsync(entityType, entityId, request.WorkflowDefinitionCode, ctx, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleError(result.Error!);
    }

    [HttpGet("{entityType}/{entityId:guid}/transitions")]
    public async Task<IActionResult> GetAvailableTransitions(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var ctx = BuildExecutionContext();
        var identityError = ValidateExecutionIdentity(ctx);
        if (identityError is not null) return identityError;
        var result = await _transitionQueryService.GetAvailableTransitionsAsync(entityType, entityId, ctx, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleError(result.Error!);
    }

    [HttpPost("{entityType}/{entityId:guid}/transitions/{transitionCode}")]
    public async Task<IActionResult> ExecuteTransition(
        string entityType,
        Guid entityId,
        string transitionCode,
        [FromBody] ExecuteTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = BuildExecutionContext();
        var identityError = ValidateExecutionIdentity(ctx);
        if (identityError is not null) return identityError;
        var transitionRequest = new WorkflowTransitionRequest(
            entityType,
            entityId,
            transitionCode,
            request.Comment,
            request.Metadata,
            request.ExpectedStateCode,
            request.ExpectedRowVersion,
            request.CorrelationId);

        var result = await _executionService.ExecuteTransitionAsync(transitionRequest, ctx, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleError(result.Error!);
    }

    [HttpGet("{entityType}/{entityId:guid}/history")]
    public async Task<IActionResult> GetHistory(
        string entityType,
        Guid entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var ctx = BuildExecutionContext();
        var identityError = ValidateExecutionIdentity(ctx);
        if (identityError is not null) return identityError;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var adapterRegistry = HttpContext.RequestServices.GetService(typeof(IWorkflowEntityAdapterRegistry)) as IWorkflowEntityAdapterRegistry;
        var adapter = adapterRegistry?.Resolve(entityType);
        if (adapter is null)
            return HandleError(WorkflowErrors.UnknownEntityType(entityType));

        var authorizeResult = await adapter.AuthorizeAsync(ctx, entityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return HandleError(authorizeResult.Error!);

        var instance = await _context.Set<WorkflowInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == ctx.TenantId && i.EntityType == entityType
                && i.EntityId == entityId && !i.IsDeleted, cancellationToken);

        if (instance is null)
            return HandleError(WorkflowErrors.WorkflowInstanceNotFound(entityType, entityId));

        var query = _context.Set<WorkflowTransitionHistory>()
            .Include(h => h.WorkflowTransition)
            .Include(h => h.FromState)
            .Include(h => h.ToState)
            .Where(h => h.WorkflowInstanceId == instance.Id)
            .OrderByDescending(h => h.ExecutedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new WorkflowHistoryEntry(
                h.Id,
                h.WorkflowTransition.Code,
                h.WorkflowTransition.NameEn,
                h.WorkflowTransition.NameAr,
                h.FromState.Code,
                h.FromState.NameEn,
                h.FromState.NameAr,
                h.ToState.Code,
                h.ToState.NameEn,
                h.ToState.NameAr,
                h.ExecutedBy,
                h.ExecutedByNameSnapshot,
                h.ExecutedAt,
                h.Comment,
                h.MetadataJson,
                h.CorrelationId))
            .ToListAsync(cancellationToken);

        var response = new WorkflowHistoryResponse(items, page, pageSize, total);
        return Ok(response);
    }

    private WorkflowExecutionContext BuildExecutionContext()
    {
        var tenantIdClaim = User.FindFirst("tenantId")?.Value;
        var tenantId = Guid.TryParse(tenantIdClaim, out var parsed) ? parsed : Guid.Empty;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("userId")?.Value
            ?? "unknown";
        var userName = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst("preferred_username")?.Value;

        var readOnly = User.FindFirst("readOnly")?.Value == "true" || User.FindFirst("viewAs")?.Value == "true";

        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();

        var permissions = User.FindAll("permission").Select(claim => claim.Value).ToArray();
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var metadata = new Dictionary<string, string?>
        {
            ["permissions"] = JsonSerializer.Serialize(permissions),
            ["roles"] = JsonSerializer.Serialize(roles)
        };

        return new WorkflowExecutionContext(
            TenantId: tenantId,
            UserId: userId,
            UserName: userName,
            ReadOnly: readOnly,
            CorrelationId: correlationId,
            Comment: null,
            Metadata: metadata);
    }

    private IActionResult? ValidateExecutionIdentity(WorkflowExecutionContext context)
    {
        if (context.TenantId == Guid.Empty || !Guid.TryParse(context.UserId, out var actorId) || actorId == Guid.Empty)
            return Unauthorized(new { error = "A valid authenticated tenant and user identity are required for workflow access." });

        var managementContext = HttpContext.RequestServices.GetService(typeof(IManagementContextAccessor)) as IManagementContextAccessor;
        if (managementContext?.Current is { } current
            && (current.TenantId != context.TenantId || current.UserId != actorId))
            return Forbid();

        return null;
    }

    private IActionResult HandleError(Error error)
    {
        return error.StatusCode switch
        {
            400 => BadRequest(new { error.Code, error.Message }),
            401 => Unauthorized(new { error.Code, error.Message }),
            403 => StatusCode(403, new { error.Code, error.Message }),
            404 => NotFound(new { error.Code, error.Message }),
            409 => Conflict(new { error.Code, error.Message }),
            _ => StatusCode(500, new { error.Code, error.Message })
        };
    }
}

public sealed record ProvisionRequest(string WorkflowDefinitionCode);

public sealed record ExecuteTransitionRequest(
    string? Comment = null,
    IReadOnlyDictionary<string, string?>? Metadata = null,
    string? ExpectedStateCode = null,
    byte[]? ExpectedRowVersion = null,
    string? CorrelationId = null);
