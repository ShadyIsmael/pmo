using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Application;

public class ApprovalService : IApprovalService
{
    private readonly DbContext _context;

    public ApprovalService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<ApprovalRequest>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _context.Set<ApprovalRequest>()
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return request is null ? Result<ApprovalRequest>.Failure(Error.NotFound()) : Result<ApprovalRequest>.Success(request);
    }

    public async Task<PagedResult<ApprovalRequest>> GetPagedAsync(int page, int pageSize, string? status = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ApprovalRequest>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (userId.HasValue) query = query.Where(a => a.RequestedBy == userId || a.CurrentApproverId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<ApprovalRequest>(items, page, pageSize, total);
    }
}
