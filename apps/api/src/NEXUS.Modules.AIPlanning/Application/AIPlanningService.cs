using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.AIPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Application;

public class AIPlanningService : IAIPlanningService
{
    private readonly DbContext _context;
    private readonly IAIProvider _aiProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AIPlanningService(DbContext context, IAIProvider aiProvider, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _aiProvider = aiProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<AIPlanRequest>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedRequestIdentity(out var tenantId, out var userId))
            return Result<AIPlanRequest>.Failure(Error.NotFound());

        var request = await _context.Set<AIPlanRequest>()
            .Include(r => r.Responses)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && r.RequestedBy == userId, cancellationToken);
        return request is null ? Result<AIPlanRequest>.Failure(Error.NotFound()) : Result<AIPlanRequest>.Success(request);
    }

    public async Task<PagedResult<AIPlanRequest>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (!TryGetTrustedRequestIdentity(out var tenantId, out var userId))
            return new PagedResult<AIPlanRequest>(Array.Empty<AIPlanRequest>(), page, pageSize, 0);

        var query = _context.Set<AIPlanRequest>()
            .Where(r => r.TenantId == tenantId && r.RequestedBy == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AIPlanRequest>(items, page, pageSize, total);
    }

    public async Task<Result<AIPlanResponse>> GeneratePlanAsync(PlanGenerationCommand command, CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedRequestIdentity(out var tenantId, out var userId))
            return Result<AIPlanResponse>.Failure(Error.Forbidden("A valid tenant and user context are required."));

        var request = new AIPlanRequest
        {
            TenantId = tenantId,
            Title = command.Title,
            Description = command.Title,
            Context = command.Context,
            Provider = command.Provider ?? "Mock",
            Model = command.Model,
            Parameters = command.Parameters,
            Status = "Processing",
            RequestedBy = userId,
            RequestedAt = DateTime.UtcNow
        };

        _context.Set<AIPlanRequest>().Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        var result = await _aiProvider.GeneratePlanAsync(command.Context, command.Parameters, cancellationToken);
        if (result.IsFailure)
        {
            request.Status = "Failed";
            request.ErrorMessage = result.Error?.Message;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<AIPlanResponse>.Failure(result.Error!);
        }

        var response = new AIPlanResponse
        {
            TenantId = tenantId,
            AIPlanRequestId = request.Id,
            Content = result.Value!,
            Summary = result.Value?.Length > 200 ? result.Value[..200] + "..." : result.Value,
            GeneratedAt = DateTime.UtcNow
        };

        _context.Set<AIPlanResponse>().Add(response);
        request.Status = "Completed";
        request.CompletedAt = DateTime.UtcNow;
        request.Result = result.Value;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<AIPlanResponse>.Success(response);
    }

    private bool TryGetTrustedRequestIdentity(out Guid tenantId, out Guid userId)
    {
        tenantId = Guid.Empty;
        userId = Guid.Empty;

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue("tenantId"), out tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out userId)
            || userId == Guid.Empty)
        {
            tenantId = Guid.Empty;
            userId = Guid.Empty;
            return false;
        }

        return true;
    }
}
