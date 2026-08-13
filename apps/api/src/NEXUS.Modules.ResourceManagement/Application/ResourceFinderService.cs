using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ResourceFinderService : IResourceFinderService
{
    private readonly DbContext _context;
    private readonly ICapacityService _capacityService;

    public ResourceFinderService(DbContext context, ICapacityService capacityService)
    {
        _context = context;
        _capacityService = capacityService;
    }

    public async Task<PagedResult<ResourceFinderResultDto>> FindAsync(ResourceFinderQueryDto query, CancellationToken cancellationToken = default)
    {
        var resourcesQuery = _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .Where(r => !r.IsDeleted && r.IsActive);

        if (!string.IsNullOrEmpty(query.Role))
            resourcesQuery = resourcesQuery.Where(r => r.JobTitle != null && r.JobTitle.ToLower().Contains(query.Role.ToLower()));
        if (!string.IsNullOrEmpty(query.Seniority))
            resourcesQuery = resourcesQuery.Where(r => r.Seniority == query.Seniority);
        if (!string.IsNullOrEmpty(query.ResourceType))
            resourcesQuery = resourcesQuery.Where(r => r.Type == query.ResourceType);
        if (query.DepartmentId.HasValue)
            resourcesQuery = resourcesQuery.Where(r => r.DepartmentId == query.DepartmentId);
        if (query.LocationId.HasValue)
            resourcesQuery = resourcesQuery.Where(r => r.LocationId == query.LocationId);
        if (query.VendorId.HasValue)
            resourcesQuery = resourcesQuery.Where(r => r.VendorId == query.VendorId);

        if (query.SkillIds is { Count: > 0 })
        {
            foreach (var skillId in query.SkillIds)
            {
                var sid = skillId;
                resourcesQuery = resourcesQuery.Where(r => r.Skills.Any(s => s.SkillId == sid));
            }
        }

        var total = await resourcesQuery.CountAsync(cancellationToken);
        var resources = await resourcesQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var startDate = query.StartDate ?? DateTime.UtcNow;
        var endDate = query.EndDate ?? startDate.AddMonths(3);

        var allAllocations = await _context.Set<ResourceAllocation>()
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>().Include(c => c.Holidays).ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(c => c.IsDefault) ?? new WorkingCalendar { HoursPerDay = 8, WorkingDaysPerWeek = 5 };

        var results = resources.Select(r =>
        {
            var resourceAllocs = allAllocations.Where(a => a.ResourceId == r.Id).ToList();
            var grossCapacity = CalculateGrossCapacity(startDate, endDate, defaultCalendar);
            var confirmedHours = resourceAllocs.Where(a => a.Status == ResourceAllocation.StatusConfirmed)
                .Sum(a => a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossCapacity));
            var availableHours = Math.Max(0, grossCapacity - confirmedHours);

            var conflictingAllocs = resourceAllocs
                .Where(a => a.StartDate < endDate && a.EndDate > (query.StartDate ?? startDate))
                .Select(a => new ResourceFinderConflictDto
                {
                    AllocationId = a.Id,
                    ProjectName = a.ProjectName ?? "Unnamed",
                    Role = a.Role,
                    AllocationPercentage = a.AllocationPercentage,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate
                }).ToList();

            var skillMatches = (query.SkillIds ?? new List<Guid>()).Select(skillId =>
            {
                var rs = r.Skills.FirstOrDefault(s => s.SkillId == skillId);
                return new ResourceFinderSkillMatchDto
                {
                    SkillName = rs?.Skill?.Name ?? "Unknown",
                    Proficiency = rs?.Proficiency ?? "N/A",
                    IsMatch = rs != null
                };
            }).ToList();

            var matchScore = CalculateMatchScore(r, query, skillMatches, availableHours, grossCapacity, defaultCalendar);

            return new ResourceFinderResultDto
            {
                ResourceId = r.Id,
                ResourceName = r.Name,
                JobTitle = r.JobTitle,
                Seniority = r.Seniority,
                Email = r.Email,
                Phone = r.Phone,
                ResourceType = r.Type,
                AvatarUrl = r.AvatarUrl,
                MatchScore = matchScore,
                AvailableCapacityHours = Math.Round(availableHours, 1),
                CurrentLoadPercent = grossCapacity > 0 ? Math.Round(confirmedHours / grossCapacity * 100, 1) : 0,
                EarliestAvailableDate = CalculateEarliestDate(r, resourceAllocs, query.StartDate ?? startDate),
                ConflictingAllocations = conflictingAllocs,
                SkillMatches = skillMatches
            };
        })
        .OrderByDescending(r => r.MatchScore)
        .ThenByDescending(r => r.AvailableCapacityHours)
        .ToList();

        return new PagedResult<ResourceFinderResultDto>(results, query.Page, query.PageSize, total);
    }

    private static decimal CalculateMatchScore(Resource resource, ResourceFinderQueryDto query, List<ResourceFinderSkillMatchDto> skillMatches, decimal availableHours, decimal grossCapacity, WorkingCalendar calendar)
    {
        decimal score = 50;

        if (!string.IsNullOrEmpty(query.Role) && resource.JobTitle != null &&
            resource.JobTitle.Contains(query.Role, StringComparison.OrdinalIgnoreCase))
            score += 15;

        if (!string.IsNullOrEmpty(query.Seniority) && resource.Seniority == query.Seniority)
            score += 10;

        if (query.SkillIds is { Count: > 0 })
        {
            var matchCount = skillMatches.Count(s => s.IsMatch);
            score += (decimal)matchCount / query.SkillIds.Count * 20;
        }

        if (query.RequiredAllocation.HasValue && grossCapacity > 0)
        {
            var availableRatio = availableHours / grossCapacity;
            var requiredRatio = query.RequiredAllocation.Value / 100m;
            if (availableRatio >= requiredRatio) score += 15;
            else score += availableRatio / requiredRatio * 15;
        }

        if (!string.IsNullOrEmpty(query.ResourceType) && resource.Type == query.ResourceType)
            score += 5;

        return Math.Round(Math.Min(100, score), 1);
    }

    private static decimal CalculateGrossCapacity(DateTime start, DateTime end, WorkingCalendar calendar)
    {
        var days = (end - start).Days;
        var workingDays = days > 7
            ? (days / 7) * calendar.WorkingDaysPerWeek + Math.Min(days % 7, calendar.WorkingDaysPerWeek)
            : Math.Min(days, calendar.WorkingDaysPerWeek);
        return workingDays * calendar.HoursPerDay;
    }

    private static DateTime? CalculateEarliestDate(Resource resource, List<ResourceAllocation> allocations, DateTime from)
    {
        var futureAllocs = allocations.Where(a => a.EndDate >= from).OrderBy(a => a.EndDate).ToList();
        if (futureAllocs.Count == 0) return from;
        var currentDate = from;
        foreach (var alloc in futureAllocs)
        {
            if (alloc.StartDate > currentDate) return currentDate;
            currentDate = alloc.EndDate > currentDate ? alloc.EndDate : currentDate;
        }
        return currentDate;
    }
}
