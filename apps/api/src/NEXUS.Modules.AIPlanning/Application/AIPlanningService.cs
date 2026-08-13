using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.AIPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Application;

public class AIPlanningService : IAIPlanningService
{
    private readonly DbContext _context;
    private readonly IAIProvider _aiProvider;

    public AIPlanningService(DbContext context, IAIProvider aiProvider)
    {
        _context = context;
        _aiProvider = aiProvider;
    }

    public async Task<Result<AIPlanRequest>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _context.Set<AIPlanRequest>()
            .Include(r => r.Responses)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return request is null ? Result<AIPlanRequest>.Failure(Error.NotFound()) : Result<AIPlanRequest>.Success(request);
    }

    public async Task<PagedResult<AIPlanRequest>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<AIPlanRequest>();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AIPlanRequest>(items, page, pageSize, total);
    }

    public async Task<Result<AIPlanResponse>> GeneratePlanAsync(PlanGenerationCommand command, CancellationToken cancellationToken = default)
    {
        var request = new AIPlanRequest
        {
            Title = command.Title,
            Description = command.Title,
            Context = command.Context,
            Provider = command.Provider ?? "Mock",
            Model = command.Model,
            Parameters = command.Parameters,
            Status = "Processing",
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
}
