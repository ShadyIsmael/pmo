using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public sealed class MeetingPublicIdLookup(DbContext context) : IMeetingPublicIdLookup
{
    public Task<Guid?> FindPublicIdAsync(Guid meetingIdOrPublicId, Guid tenantId, CancellationToken cancellationToken = default) =>
        context.Set<Meeting>().IgnoreQueryFilters().AsNoTracking()
            .Where(meeting => meeting.TenantId == tenantId && !meeting.IsDeleted
                && (meeting.Id == meetingIdOrPublicId || meeting.PublicId == meetingIdOrPublicId))
            .Select(meeting => (Guid?)meeting.PublicId)
            .FirstOrDefaultAsync(cancellationToken);
}
