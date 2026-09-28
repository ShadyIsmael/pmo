using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanningCalendarService : IPlanningCalendarService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlanSchedulingService _schedulingService;

    public PlanningCalendarService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IPlanSchedulingService? schedulingService = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _schedulingService = schedulingService ?? new PlanSchedulingService(context, new Domain.Scheduling.AutoSchedulingEngine());
    }

    public async Task<Result<IReadOnlyList<CalendarTemplateDto>>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await TemplateQuery().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CalendarTemplateDto>>.Success(templates.Select(t => t.ToDto()).ToList());
    }

    public async Task<Result<CalendarTemplateDto>> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await TemplateQuery().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        return template is null
            ? Result<CalendarTemplateDto>.Failure(Error.NotFound("Calendar template not found"))
            : Result<CalendarTemplateDto>.Success(template.ToDto());
    }

    public async Task<Result<CalendarTemplateDto>> CreateTemplateAsync(CreateCalendarTemplateDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Result<CalendarTemplateDto>.Failure(Error.Validation("Calendar template name is required"));

        var definitionError = NormalizeDefinition(dto.DayRules, dto.Holidays, null, null, out var dayRules, out var holidays);
        if (definitionError is not null) return Result<CalendarTemplateDto>.Failure(definitionError);

        if (await TemplateQuery().AnyAsync(t => t.Name == name, cancellationToken))
            return Result<CalendarTemplateDto>.Failure(Error.Conflict($"A calendar template named '{name}' already exists"));

        var template = new CalendarTemplate
        {
            TenantId = GetCurrentTenantId(),
            Name = name,
            Description = dto.Description?.Trim(),
            CreatedBy = userId
        };
        AddDefinition(template, dayRules, holidays, userId);
        _context.Set<CalendarTemplate>().Add(template);

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Result<CalendarTemplateDto>.Failure(Error.Conflict("The calendar template could not be created")); }

        await AuditAsync("CalendarTemplate", template.Id, "Create", null, dto, userId, cancellationToken);
        return Result<CalendarTemplateDto>.Success(template.ToDto());
    }

    public async Task<Result<CalendarTemplateDto>> UpdateTemplateAsync(Guid templateId, UpdateCalendarTemplateDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var template = await TemplateQuery().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (template is null) return Result<CalendarTemplateDto>.Failure(Error.NotFound("Calendar template not found"));

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Result<CalendarTemplateDto>.Failure(Error.Validation("Calendar template name is required"));
        if (await TemplateQuery().AnyAsync(t => t.Id != templateId && t.Name == name, cancellationToken))
            return Result<CalendarTemplateDto>.Failure(Error.Conflict($"A calendar template named '{name}' already exists"));

        var definitionError = NormalizeDefinition(dto.DayRules, dto.Holidays, null, null, out var dayRules, out var holidays);
        if (definitionError is not null) return Result<CalendarTemplateDto>.Failure(definitionError);

        var original = template.ToDto();
        await ReplaceDefinitionAsync(template, dayRules, holidays, userId, cancellationToken);
        template.Name = name;
        template.Description = dto.Description?.Trim();
        template.UpdatedBy = userId;
        _context.Entry(template).Property(t => t.RowVersion).OriginalValue = dto.RowVersion;

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<CalendarTemplateDto>.Failure(Error.Conflict("The calendar template was modified by another user. Reload and try again.")); }

        await AuditAsync("CalendarTemplate", template.Id, "Update", original, template.ToDto(), userId, cancellationToken);
        return Result<CalendarTemplateDto>.Success(template.ToDto());
    }

    public async Task<Result> DeleteTemplateAsync(Guid templateId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var template = await TemplateQuery().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (template is null) return Result.Failure(Error.NotFound("Calendar template not found"));
        if (await _context.Set<PlanCalendar>().AnyAsync(c => c.TemplateId == templateId, cancellationToken))
            return Result.Failure(Error.Conflict("A calendar template referenced by a plan cannot be deleted"));

        template.IsDeleted = true;
        template.UpdatedBy = userId;
        await SoftDeleteDefinitionChildrenAsync(template, userId, cancellationToken);
        _context.Entry(template).Property(t => t.RowVersion).OriginalValue = rowVersion;

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The calendar template was modified by another user. Reload and try again.")); }

        await AuditAsync("CalendarTemplate", template.Id, "Delete", null, null, userId, cancellationToken);
        return Result.Success();
    }

    public Task<Result<IReadOnlyList<PlanCalendarDto>>> GetByPlanAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default) =>
        GetByVersionInternalAsync(projectId, planId, null, true, cancellationToken);

    public Task<Result<IReadOnlyList<PlanCalendarDto>>> GetByVersionAsync(Guid projectId, Guid planId, Guid planVersionId, CancellationToken cancellationToken = default) =>
        GetByVersionInternalAsync(projectId, planId, planVersionId, false, cancellationToken);

    public async Task<Result<PlanCalendarDto>> CreatePlanCalendarAsync(Guid projectId, Guid planId, CreatePlanCalendarDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var versionResult = await GetVersionForMutationAsync(projectId, planId, cancellationToken);
        if (versionResult.IsFailure) return Result<PlanCalendarDto>.Failure(versionResult.Error!);
        var version = versionResult.Value!;

        var template = await GetTemplateForReferenceAsync(dto.TemplateId, cancellationToken);
        if (dto.TemplateId.HasValue && template is null)
            return Result<PlanCalendarDto>.Failure(Error.Validation("Calendar template must belong to the current tenant"));

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = template?.Name;
        if (string.IsNullOrWhiteSpace(name)) return Result<PlanCalendarDto>.Failure(Error.Validation("Plan calendar name is required"));
        if (await CalendarQuery(version.Id).AnyAsync(c => c.Name == name, cancellationToken))
            return Result<PlanCalendarDto>.Failure(Error.Conflict($"A plan calendar named '{name}' already exists in this version"));

        var sourceDays = dto.DayRules.Count == 0 && template is not null ? template.DayRules.Where(rule => !rule.IsDeleted).Select(ToDto).ToList() : dto.DayRules;
        var sourceHolidays = dto.Holidays.Count == 0 && template is not null ? template.Holidays.Where(holiday => !holiday.IsDeleted).Select(ToDto).ToList() : dto.Holidays;
        var definitionError = NormalizeDefinition(sourceDays, sourceHolidays, dto.EffectiveFrom, dto.EffectiveTo, out var dayRules, out var holidays);
        if (definitionError is not null) return Result<PlanCalendarDto>.Failure(definitionError);

        var calendar = new PlanCalendar
        {
            TenantId = GetCurrentTenantId(),
            ProjectId = projectId,
            PlanVersionId = version.Id,
            TemplateId = dto.TemplateId,
            Name = name,
            Description = dto.Description?.Trim(),
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsDefault = dto.IsDefault == true || !await CalendarQuery(version.Id).AnyAsync(cancellationToken),
            CreatedBy = userId
        };
        if (calendar.IsDefault)
        {
            foreach (var existing in await CalendarQuery(version.Id).Where(item => item.IsDefault).ToListAsync(cancellationToken))
            {
                existing.IsDefault = false;
                existing.UpdatedBy = userId;
            }
        }
        AddDefinition(calendar, dayRules, holidays, userId);
        _context.Set<PlanCalendar>().Add(calendar);

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Result<PlanCalendarDto>.Failure(Error.Conflict("The plan calendar could not be created")); }

        var schedule = await RecalculateForCalendarAsync(calendar, userId, cancellationToken, calendar.IsDefault);
        if (schedule.IsFailure) return Result<PlanCalendarDto>.Failure(schedule.Error!);

        await AuditAsync("PlanCalendar", calendar.Id, "Create", null, dto, userId, cancellationToken);
        return Result<PlanCalendarDto>.Success(calendar.ToDto());
    }

    public async Task<Result<PlanCalendarDto>> UpdatePlanCalendarAsync(Guid projectId, Guid planId, Guid calendarId, UpdatePlanCalendarDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var versionResult = await GetVersionForMutationAsync(projectId, planId, cancellationToken);
        if (versionResult.IsFailure) return Result<PlanCalendarDto>.Failure(versionResult.Error!);
        var version = versionResult.Value!;
        var calendar = await CalendarByIdQuery(calendarId).FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
        if (calendar is null) return Result<PlanCalendarDto>.Failure(Error.NotFound("Plan calendar not found"));
        // Validate the caller's optimistic-concurrency token against the value
        // read in this request. Keep EF's original value sourced from the
        // tracked entity itself; assigning a deserialized byte[] directly to
        // OriginalValue can produce a false concurrency miss with SQL Server
        // rowversion values.
        var currentRowVersion = calendar.RowVersion.ToArray();
        if (dto.RowVersion is null || dto.RowVersion.Length == 0 || !currentRowVersion.SequenceEqual(dto.RowVersion))
            return Result<PlanCalendarDto>.Failure(Error.Conflict("The plan calendar was modified by another user. Reload and try again."));
        var calendarVersion = await _context.Set<PlanVersion>().FirstOrDefaultAsync(v => v.Id == calendar.PlanVersionId && v.PlanId == planId, cancellationToken);
        if (calendarVersion is null) return Result<PlanCalendarDto>.Failure(Error.NotFound("Plan calendar not found"));
        if (calendar.PlanVersionId != version.Id)
            return Result<PlanCalendarDto>.Failure(calendarVersion.Type == PlanVersionType.Baseline
                ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
                : Error.Validation("Only the active Working version is editable"));

        var template = await GetTemplateForReferenceAsync(dto.TemplateId, cancellationToken);
        if (dto.TemplateId.HasValue && template is null)
            return Result<PlanCalendarDto>.Failure(Error.Validation("Calendar template must belong to the current tenant"));

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = template?.Name ?? calendar.Name;
        if (await CalendarQuery(version.Id).AnyAsync(c => c.Id != calendarId && c.Name == name, cancellationToken))
            return Result<PlanCalendarDto>.Failure(Error.Conflict($"A plan calendar named '{name}' already exists in this version"));

        var sourceDays = dto.DayRules.Count == 0 && template is not null ? template.DayRules.Where(rule => !rule.IsDeleted).Select(ToDto).ToList() : dto.DayRules;
        var sourceHolidays = dto.Holidays.Count == 0 && template is not null ? template.Holidays.Where(holiday => !holiday.IsDeleted).Select(ToDto).ToList() : dto.Holidays;
        var definitionError = NormalizeDefinition(sourceDays, sourceHolidays, dto.EffectiveFrom, dto.EffectiveTo, out var dayRules, out var holidays);
        if (definitionError is not null) return Result<PlanCalendarDto>.Failure(definitionError);

        var original = calendar.ToDto();
        var wasDefault = calendar.IsDefault;
        await ReplaceDefinitionAsync(calendar, dayRules, holidays, userId, cancellationToken);
        calendar.TemplateId = dto.TemplateId;
        calendar.Name = name;
        calendar.Description = dto.Description?.Trim();
        calendar.EffectiveFrom = dto.EffectiveFrom;
        calendar.EffectiveTo = dto.EffectiveTo;
        if (dto.IsDefault.HasValue)
        {
            calendar.IsDefault = dto.IsDefault.Value;
            if (calendar.IsDefault)
            {
                foreach (var existing in await CalendarQuery(version.Id).Where(item => item.Id != calendar.Id && item.IsDefault).ToListAsync(cancellationToken))
                {
                    existing.IsDefault = false;
                    existing.UpdatedBy = userId;
                }
            }
        }
        calendar.UpdatedBy = userId;
        _context.Entry(calendar).Property(c => c.RowVersion).OriginalValue = currentRowVersion;

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<PlanCalendarDto>.Failure(Error.Conflict("The plan calendar was modified by another user. Reload and try again.")); }

        var schedule = await RecalculateForCalendarAsync(calendar, userId, cancellationToken, wasDefault || calendar.IsDefault);
        if (schedule.IsFailure) return Result<PlanCalendarDto>.Failure(schedule.Error!);

        await AuditAsync("PlanCalendar", calendar.Id, "Update", original, calendar.ToDto(), userId, cancellationToken);
        return Result<PlanCalendarDto>.Success(calendar.ToDto());
    }

    public async Task<Result> DeletePlanCalendarAsync(Guid projectId, Guid planId, Guid calendarId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var versionResult = await GetVersionForMutationAsync(projectId, planId, cancellationToken);
        if (versionResult.IsFailure) return Result.Failure(versionResult.Error!);
        var calendar = await CalendarByIdQuery(calendarId).FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
        if (calendar is null) return Result.Failure(Error.NotFound("Plan calendar not found"));
        var calendarVersion = await _context.Set<PlanVersion>().FirstOrDefaultAsync(v => v.Id == calendar.PlanVersionId && v.PlanId == planId, cancellationToken);
        if (calendarVersion is null) return Result.Failure(Error.NotFound("Plan calendar not found"));
        if (calendar.PlanVersionId != versionResult.Value!.Id)
            return Result.Failure(calendarVersion.Type == PlanVersionType.Baseline
                ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
                : Error.Validation("Only the active Working version is editable"));

        if (await _context.Set<PlanActivity>().AnyAsync(activity => activity.CalendarId == calendar.Id, cancellationToken))
            return Result.Failure(Error.Validation("This calendar is assigned to one or more activities and cannot be deleted"));

        var wasDefault = calendar.IsDefault;
        calendar.IsDeleted = true;
        calendar.IsDefault = false;
        calendar.UpdatedBy = userId;
        await SoftDeleteDefinitionChildrenAsync(calendar, userId, cancellationToken);
        _context.Entry(calendar).Property(c => c.RowVersion).OriginalValue = rowVersion;

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The plan calendar was modified by another user. Reload and try again.")); }

        var schedule = await RecalculateForCalendarAsync(calendar, userId, cancellationToken, wasDefault);
        if (schedule.IsFailure) return schedule;

        await AuditAsync("PlanCalendar", calendar.Id, "Delete", null, null, userId, cancellationToken);
        return Result.Success();
    }

    private IQueryable<CalendarTemplate> TemplateQuery() =>
        _context.Set<CalendarTemplate>().Include(t => t.DayRules).Include(t => t.Holidays).Where(t => !t.IsDeleted);

    private IQueryable<PlanCalendar> CalendarQuery(Guid planVersionId) =>
        _context.Set<PlanCalendar>().Include(c => c.DayRules).Include(c => c.Holidays).Where(c => c.PlanVersionId == planVersionId && !c.IsDeleted);

    private IQueryable<PlanCalendar> CalendarByIdQuery(Guid calendarId) =>
        _context.Set<PlanCalendar>().Include(c => c.DayRules).Include(c => c.Holidays).Where(c => c.Id == calendarId && !c.IsDeleted);

    private async Task<Result<IReadOnlyList<PlanCalendarDto>>> GetByVersionInternalAsync(Guid projectId, Guid planId, Guid? versionId, bool activeWorking, CancellationToken cancellationToken)
    {
        var version = await GetVersionAsync(projectId, planId, versionId, activeWorking, cancellationToken);
        if (version is null) return Result<IReadOnlyList<PlanCalendarDto>>.Failure(await VersionNotFoundErrorAsync(planId, activeWorking, cancellationToken));
        var calendars = await CalendarQuery(version.Id).AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PlanCalendarDto>>.Success(calendars.Select(c => c.ToDto()).ToList());
    }

    private async Task<PlanVersion?> GetVersionAsync(Guid projectId, Guid planId, Guid? versionId, bool activeWorking, CancellationToken cancellationToken)
    {
        var query = _context.Set<PlanVersion>().Include(v => v.Plan).Where(v => v.PlanId == planId && v.Plan!.ProjectId == projectId);
        if (versionId.HasValue) query = query.Where(v => v.Id == versionId.Value);
        else if (activeWorking) query = query.Where(v => v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Result<PlanVersion>> GetVersionForMutationAsync(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var version = await GetVersionAsync(projectId, planId, null, true, cancellationToken);
        if (version is null) return Result<PlanVersion>.Failure(await VersionNotFoundErrorAsync(planId, true, cancellationToken));
        return Result<PlanVersion>.Success(version);
    }

    private async Task<Error> VersionNotFoundErrorAsync(Guid planId, bool activeWorking, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        return !planExists ? Error.NotFound("Plan not found") : activeWorking ? Error.NotFound("No active working version exists for this plan") : Error.NotFound("Plan version not found");
    }

    private async Task<CalendarTemplate?> GetTemplateForReferenceAsync(Guid? templateId, CancellationToken cancellationToken) =>
        templateId.HasValue ? await TemplateQuery().FirstOrDefaultAsync(t => t.Id == templateId.Value, cancellationToken) : null;

    private static Error? NormalizeDefinition(
        IEnumerable<CalendarDayRuleDto> inputDays,
        IEnumerable<CalendarHolidayDto> inputHolidays,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        out List<CalendarDayRuleDto> days,
        out List<CalendarHolidayDto> holidays)
    {
        days = new List<CalendarDayRuleDto>();
        holidays = new List<CalendarHolidayDto>();
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo < effectiveFrom)
            return Error.Validation("Calendar effective end date cannot be earlier than its start date");

        var suppliedDays = inputDays.ToList();
        if (suppliedDays.Count == 0)
        {
            suppliedDays = Enum.GetValues<DayOfWeek>().Select(day => new CalendarDayRuleDto
            {
                DayOfWeek = day,
                IsWorkingDay = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
                StartTime = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday ? new TimeSpan(8, 0, 0) : null,
                EndTime = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday ? new TimeSpan(17, 0, 0) : null,
                BreakStartTime = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday ? new TimeSpan(12, 0, 0) : null,
                BreakEndTime = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday ? new TimeSpan(13, 0, 0) : null
            }).ToList();
        }
        if (suppliedDays.Count > 7 || suppliedDays.Select(d => d.DayOfWeek).Distinct().Count() != suppliedDays.Count)
            return Error.Validation("Calendar must contain at most one rule for each day of the week");

        foreach (var day in suppliedDays)
        {
            if (day.IsWorkingDay)
            {
                if (!day.StartTime.HasValue || !day.EndTime.HasValue || day.EndTime <= day.StartTime)
                    return Error.Validation($"Working day {day.DayOfWeek} must have a valid start and end time");
                if (day.BreakStartTime.HasValue != day.BreakEndTime.HasValue ||
                    (day.BreakStartTime.HasValue && (day.BreakStartTime < day.StartTime || day.BreakEndTime > day.EndTime || day.BreakEndTime <= day.BreakStartTime)))
                    return Error.Validation($"Working day {day.DayOfWeek} has an invalid break interval");
            }
            else if (day.StartTime.HasValue || day.EndTime.HasValue || day.BreakStartTime.HasValue || day.BreakEndTime.HasValue)
            {
                return Error.Validation($"Non-working day {day.DayOfWeek} cannot define working times");
            }
            days.Add(day);
        }

        foreach (var dayOfWeek in Enum.GetValues<DayOfWeek>())
            if (days.All(d => d.DayOfWeek != dayOfWeek)) days.Add(new CalendarDayRuleDto { DayOfWeek = dayOfWeek });

        if (days.All(day => !day.IsWorkingDay)) return Error.Validation("Calendar must contain at least one working day");

        var suppliedHolidays = inputHolidays.ToList();
        if (suppliedHolidays.Any(h => string.IsNullOrWhiteSpace(h.Name))) return Error.Validation("Calendar holiday name is required");
        if (suppliedHolidays.Any(h => h.EndDate.HasValue && h.EndDate.Value.Date < h.Date.Date)) return Error.Validation("Calendar holiday end date cannot be earlier than its start date");
        if (suppliedHolidays.Select(h => h.Date.Date).Distinct().Count() != suppliedHolidays.Count)
            return Error.Conflict("A calendar cannot contain duplicate holiday start dates");
        holidays = suppliedHolidays.Select(h => new CalendarHolidayDto { Name = h.Name.Trim(), Description = h.Description?.Trim(), Date = h.Date.Date, EndDate = h.EndDate?.Date, IsRecurring = h.IsRecurring }).ToList();
        return null;
    }

    private static void AddDefinition(PlanningCalendar calendar, IEnumerable<CalendarDayRuleDto> days, IEnumerable<CalendarHolidayDto> holidays, string userId)
    {
        foreach (var day in days) calendar.DayRules.Add(ToEntity(day, calendar.Id, calendar.TenantId, userId));
        foreach (var holiday in holidays) calendar.Holidays.Add(ToEntity(holiday, calendar.Id, calendar.TenantId, userId));
    }

    private async Task ReplaceDefinitionAsync(PlanningCalendar calendar, IEnumerable<CalendarDayRuleDto> days, IEnumerable<CalendarHolidayDto> holidays, string userId, CancellationToken cancellationToken)
    {
        await SoftDeleteDefinitionChildrenAsync(calendar, userId, cancellationToken);
        AddDefinition(calendar, days, holidays, userId);
        foreach (var rule in calendar.DayRules) _context.Entry(rule).State = EntityState.Added;
        foreach (var holiday in calendar.Holidays) _context.Entry(holiday).State = EntityState.Added;
    }

    private async Task SoftDeleteDefinitionChildrenAsync(PlanningCalendar calendar, string userId, CancellationToken cancellationToken)
    {
        // Child rowversions are not part of the calendar DTO. Update the
        // governed child snapshot set-wise so replacing a calendar definition
        // does not produce a false concurrency conflict on every day rule.
        var now = DateTime.UtcNow;
        await _context.Set<CalendarDayRule>()
            .Where(rule => rule.PlanningCalendarId == calendar.Id && !rule.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rule => rule.IsDeleted, true)
                .SetProperty(rule => rule.UpdatedBy, userId)
                .SetProperty(rule => rule.UpdatedAt, now), cancellationToken);
        await _context.Set<CalendarHoliday>()
            .Where(holiday => holiday.PlanningCalendarId == calendar.Id && !holiday.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(holiday => holiday.IsDeleted, true)
                .SetProperty(holiday => holiday.UpdatedBy, userId)
                .SetProperty(holiday => holiday.UpdatedAt, now), cancellationToken);

        foreach (var rule in calendar.DayRules.ToList()) _context.Entry(rule).State = EntityState.Detached;
        foreach (var holiday in calendar.Holidays.ToList()) _context.Entry(holiday).State = EntityState.Detached;
        calendar.DayRules = new List<CalendarDayRule>();
        calendar.Holidays = new List<CalendarHoliday>();
    }

    private static CalendarDayRule ToEntity(CalendarDayRuleDto dto, Guid calendarId, Guid tenantId, string userId) => new()
    {
        TenantId = tenantId, PlanningCalendarId = calendarId, DayOfWeek = dto.DayOfWeek, IsWorkingDay = dto.IsWorkingDay,
        StartTime = dto.StartTime, EndTime = dto.EndTime, BreakStartTime = dto.BreakStartTime, BreakEndTime = dto.BreakEndTime,
        CreatedBy = userId
    };

    private static CalendarHoliday ToEntity(CalendarHolidayDto dto, Guid calendarId, Guid tenantId, string userId) => new()
    {
        TenantId = tenantId, PlanningCalendarId = calendarId, Name = dto.Name.Trim(), Description = dto.Description?.Trim(), Date = dto.Date.Date, EndDate = dto.EndDate?.Date, IsRecurring = dto.IsRecurring, CreatedBy = userId
    };

    private static CalendarDayRuleDto ToDto(CalendarDayRule rule) => new()
    {
        DayOfWeek = rule.DayOfWeek, IsWorkingDay = rule.IsWorkingDay, StartTime = rule.StartTime, EndTime = rule.EndTime,
        BreakStartTime = rule.BreakStartTime, BreakEndTime = rule.BreakEndTime
    };

    private static CalendarHolidayDto ToDto(CalendarHoliday holiday) => new() { Name = holiday.Name, Description = holiday.Description, Date = holiday.Date, EndDate = holiday.EndDate, IsRecurring = holiday.IsRecurring };

    private async Task<Result> RecalculateForCalendarAsync(PlanCalendar calendar, string userId, CancellationToken cancellationToken, bool includeDefaultActivities = false)
    {
        if (_context.Model.FindEntityType(typeof(PlanActivity)) is null) return Result.Success();
        var activityIds = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == calendar.PlanVersionId &&
                (activity.CalendarId == calendar.Id || (includeDefaultActivities && activity.CalendarId == null)))
            .Select(activity => activity.Id)
            .ToListAsync(cancellationToken);
        if (activityIds.Count == 0) return Result.Success();
        var schedule = await _schedulingService.RecalculateAsync(calendar.PlanVersionId, activityIds.Select(id => new SchedulingSeed(id)).ToList(), userId, cancellationToken);
        if (schedule.IsFailure) return Result.Failure(schedule.Error!);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The calendar changed while task dates were being recalculated. Reload and try again.")); }
        return Result.Success();
    }

    private Task AuditAsync(string entityType, Guid entityId, string action, object? original, object? current, string userId, CancellationToken cancellationToken) =>
        _auditService.LogAsync(new AuditEntry
        {
            EntityType = entityType, EntityId = entityId, Action = action, UserId = TryParseGuid(userId),
            OriginalValues = original is null ? null : System.Text.Json.JsonSerializer.Serialize(original),
            NewValues = current is null ? null : System.Text.Json.JsonSerializer.Serialize(current),
            Timestamp = DateTime.UtcNow, CreatedBy = userId
        }, cancellationToken);

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantId, out var parsed) ? parsed : TenantConstants.DemoTenantId;
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
