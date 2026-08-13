using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Application;

public class MeetingService : IMeetingService
{
    private readonly DbContext _context;

    public MeetingService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Meeting>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var meeting = await _context.Set<Meeting>()
            .Include(m => m.AgendaItems)
            .Include(m => m.Participants)
            .Include(m => m.Actions)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return meeting is null ? Result<Meeting>.Failure(Error.NotFound()) : Result<Meeting>.Success(meeting);
    }

    public async Task<PagedResult<Meeting>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Meeting>().AsQueryable();
        if (projectId.HasValue) query = query.Where(m => m.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.StartTime).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Meeting>(items, page, pageSize, total);
    }
}
