using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Modules.Meetings.Infrastructure;

public class MeetingRepository
{
    private readonly DbContext _context;

    public MeetingRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Meeting>()
            .Include(m => m.AgendaItems)
            .Include(m => m.Participants)
            .Include(m => m.Actions)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }
}
