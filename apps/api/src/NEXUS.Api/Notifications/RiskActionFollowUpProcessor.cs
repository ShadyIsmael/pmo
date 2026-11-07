using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXUS.Api.RiskManagement;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using ProjectEntity = NEXUS.Modules.ProjectManagement.Domain.Project;

namespace NEXUS.Api.Notifications;

/// <summary>
/// Reconciles current Risk Action state against the canonical Notifications table.
/// The existing NextFollowUpDate is the durable schedule; the stable Notification ID
/// is the atomic per-tenant/action/date/recipient delivery key.
/// </summary>
public sealed class RiskActionFollowUpProcessor
{
    private const string RiskViewPermission = "Risk.View";
    private const string NotificationReferenceType = "RiskActionFollowUpDue";
    private const string WorkerActor = "RiskActionFollowUpWorker";
    private const string LanguageSettingKey = "regional.defaultLanguage";
    private const string TimeZoneSettingKey = "regional.defaultTimeZone";
    private const string WorkingCalendarSettingKey = "workingCalendar.defaultCalendarId";

    // Diagnostics about invalid persisted schedules are stable across polling passes.
    // The set is process-local by design and does not affect retrying the reminder.
    private static readonly ConcurrentDictionary<string, byte> ReportedDiagnostics = new(StringComparer.Ordinal);

    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IManagementScopeResolver _managementScopeResolver;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ISettingDefinitionCatalog _settingCatalog;
    private readonly EffectiveConfigurationResolver _configurationResolver;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RiskActionFollowUpProcessor> _logger;

    public RiskActionFollowUpProcessor(
        AppDbContext context,
        INotificationService notificationService,
        IUserEffectivePermissionService effectivePermissions,
        IManagementScopeResolver managementScopeResolver,
        ISettingsRepository settingsRepository,
        ISettingDefinitionCatalog settingCatalog,
        EffectiveConfigurationResolver configurationResolver,
        ILogger<RiskActionFollowUpProcessor> logger,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _notificationService = notificationService;
        _effectivePermissions = effectivePermissions;
        _managementScopeResolver = managementScopeResolver;
        _settingsRepository = settingsRepository;
        _settingCatalog = settingCatalog;
        _configurationResolver = configurationResolver;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _context.Set<RiskAction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(action => !action.IsDeleted && action.NextFollowUpDate.HasValue)
            .Select(action => new CandidateReference(action.TenantId, action.Id))
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // This lets the standard tenant filters and permission/scope services run
                // with an explicit trusted tenant while the worker has no HTTP principal.
                using var trustedTenant = TrustedTenantExecutionContext.Enter(candidate.TenantId);
                delivered += await ProcessCandidateAsync(candidate, _timeProvider.GetUtcNow(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReportOnce(
                    $"processing:{candidate.TenantId:N}:{candidate.ActionId:N}:{exception.GetType().FullName}",
                    LogLevel.Error,
                    exception,
                    "Risk Action follow-up processing is deferred after an unexpected error. TenantId={TenantId} ActionId={ActionId} ErrorType={ErrorType}",
                    candidate.TenantId,
                    candidate.ActionId,
                    exception.GetType().Name);
            }
        }

        return delivered;
    }

    private async Task<int> ProcessCandidateAsync(
        CandidateReference candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await ReadCurrentAsync(candidate, cancellationToken);
        if (current is null) return 0;

        var schedule = await ResolveScheduleAsync(current, cancellationToken);
        if (schedule is null || now < schedule.DueAtUtc) return 0;

        var recipients = await ResolveCurrentRecipientsAsync(current, cancellationToken);
        if (recipients.Count == 0)
        {
            ReportOnce(
                $"recipients:{candidate.TenantId:N}:{candidate.ActionId:N}",
                LogLevel.Warning,
                null,
                "Risk Action follow-up remains pending because neither current owner resolves to an active same-tenant System User. TenantId={TenantId} ActionId={ActionId}",
                candidate.TenantId,
                candidate.ActionId);
            return 0;
        }
        var delivered = 0;
        foreach (var recipientId in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Owner, action, Risk, Project, schedule, and recipient eligibility are read
            // again immediately before authorizing and dispatching each recipient.
            var latest = await ReadCurrentAsync(candidate, cancellationToken);
            if (latest is null) continue;
            var latestSchedule = await ResolveScheduleAsync(latest, cancellationToken);
            var deliveryNow = _timeProvider.GetUtcNow();
            if (latestSchedule is null || deliveryNow < latestSchedule.DueAtUtc) continue;
            var latestRecipients = await ResolveCurrentRecipientsAsync(latest, cancellationToken);
            if (!latestRecipients.Contains(recipientId)) continue;

            var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                recipientId,
                candidate.TenantId,
                cancellationToken);
            if (!permissions.Contains(RiskViewPermission)) continue;

            if (!await _managementScopeResolver.CanAccessProjectAsync(
                    recipientId,
                    candidate.TenantId,
                    latest.Project.Id,
                    cancellationToken))
                continue;

            // Re-read after potentially expensive authorization resolution so a just-changed
            // owner or a completed/closed record cannot be notified from the stale snapshot.
            latest = await ReadCurrentAsync(candidate, cancellationToken);
            if (latest is null) continue;
            latestSchedule = await ResolveScheduleAsync(latest, cancellationToken);
            deliveryNow = _timeProvider.GetUtcNow();
            if (latestSchedule is null || deliveryNow < latestSchedule.DueAtUtc) continue;
            latestRecipients = await ResolveCurrentRecipientsAsync(latest, cancellationToken);
            if (!latestRecipients.Contains(recipientId)) continue;

            var channelEligibility = await _notificationService.GetChannelEligibilityAsync(
                candidate.TenantId,
                [recipientId],
                NotificationTypes.RiskActionFollowUpDue,
                cancellationToken,
                latest.OrganizationId);
            if (!channelEligibility.TryGetValue(recipientId, out var channels)) continue;

            deliveryNow = _timeProvider.GetUtcNow();
            if (deliveryNow < latestSchedule.DueAtUtc) continue;
            var notification = CreateNotification(latest, latestSchedule.DueAtUtc, recipientId, channels, latestSchedule.Language, deliveryNow);
            var result = await _notificationService.SendIdempotentAsync(
                notification,
                allowPush: channels.PushEnabled && channels.PushAvailable,
                allowEmail: channels.EmailEnabled && channels.EmailAvailable,
                cancellationToken);
            if (result.IsFailure)
            {
                ReportOnce(
                    $"send:{candidate.TenantId:N}:{candidate.ActionId:N}:{DateOnly.FromDateTime(latest.Action.NextFollowUpDate!.Value.Date):yyyy-MM-dd}:{recipientId:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up notification could not be persisted; it will be retried. TenantId={TenantId} ActionId={ActionId} RecipientId={RecipientId}",
                    candidate.TenantId,
                    candidate.ActionId,
                    recipientId);
                continue;
            }

            if (result.Value) delivered++;
        }

        return delivered;
    }

    private async Task<CurrentAction?> ReadCurrentAsync(CandidateReference candidate, CancellationToken cancellationToken)
    {
        var action = await _context.Set<RiskAction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Risk)
            .FirstOrDefaultAsync(item => item.Id == candidate.ActionId
                && item.TenantId == candidate.TenantId
                && !item.IsDeleted
                && item.NextFollowUpDate.HasValue
                && (item.Status == RiskActionStatus.Planned || item.Status == RiskActionStatus.InProgress)
                && item.Risk.TenantId == candidate.TenantId
                && !item.Risk.IsDeleted
                && item.Risk.Status != RiskStatus.Closed,
                cancellationToken);
        if (action is null) return null;

        var project = await _context.Set<ProjectEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == action.Risk.ProjectId
                && item.TenantId == candidate.TenantId
                && !item.IsDeleted,
                cancellationToken);
        if (project is null)
        {
            ReportOnce(
                $"project:{candidate.TenantId:N}:{candidate.ActionId:N}",
                LogLevel.Warning,
                null,
                "Risk Action follow-up is deferred because its active same-tenant Project could not be resolved. TenantId={TenantId} ActionId={ActionId}",
                candidate.TenantId,
                candidate.ActionId);
            return null;
        }

        var organization = await ResolveManagementOrganizationAsync(project, candidate.TenantId, candidate.ActionId, cancellationToken);
        if (!organization.IsValid) return null;

        return new CurrentAction(candidate.TenantId, action, project, organization.OrganizationId);
    }

    private async Task<OrganizationResolution> ResolveManagementOrganizationAsync(
        ProjectEntity project,
        Guid tenantId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        Guid? portfolioId;
        if (project.ProgramId.HasValue)
        {
            portfolioId = await _context.Set<ProgramEntity>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(program => program.Id == project.ProgramId.Value
                    && program.TenantId == tenantId
                    && !program.IsDeleted)
                .Select(program => (Guid?)program.PortfolioId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!portfolioId.HasValue)
            {
                ReportOnce(
                    $"program:{tenantId:N}:{project.ProgramId.Value:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the Project's same-tenant management Program is missing or inactive. TenantId={TenantId} ActionId={ActionId} ProgramId={ProgramId}",
                    tenantId,
                    actionId,
                    project.ProgramId.Value);
                return OrganizationResolution.Invalid;
            }
        }
        else
        {
            portfolioId = project.PortfolioId;
        }

        Guid? organizationId = null;
        if (portfolioId.HasValue)
        {
            var portfolio = await _context.Set<Portfolio>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.Id == portfolioId.Value
                    && item.TenantId == tenantId
                    && !item.IsDeleted)
                .Select(item => new { item.Id, item.OrganizationId })
                .FirstOrDefaultAsync(cancellationToken);
            if (portfolio is null)
            {
                ReportOnce(
                    $"portfolio:{tenantId:N}:{portfolioId.Value:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the Project's same-tenant management Portfolio is missing or inactive. TenantId={TenantId} ActionId={ActionId} PortfolioId={PortfolioId}",
                    tenantId,
                    actionId,
                    portfolioId.Value);
                return OrganizationResolution.Invalid;
            }

            organizationId = portfolio.OrganizationId;
        }

        if (organizationId.HasValue)
        {
            var organizationIsActive = await _context.Set<Organization>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(item => item.Id == organizationId.Value
                    && item.TenantId == tenantId
                    && item.IsActive
                    && !item.IsDeleted,
                    cancellationToken);
            if (!organizationIsActive)
            {
                ReportOnce(
                    $"organization:{tenantId:N}:{organizationId.Value:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the management Organization is missing, inactive, or outside its Tenant. TenantId={TenantId} ActionId={ActionId} OrganizationId={OrganizationId}",
                    tenantId,
                    actionId,
                    organizationId.Value);
                return OrganizationResolution.Invalid;
            }

            return OrganizationResolution.Valid(organizationId);
        }

        if (project.DepartmentId.HasValue)
        {
            var department = await _context.Set<Department>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.Id == project.DepartmentId.Value
                    && item.TenantId == tenantId
                    && item.IsActive
                    && !item.IsDeleted)
                .Select(item => new { item.Id, item.BusinessUnitId })
                .FirstOrDefaultAsync(cancellationToken);
            if (department is null)
            {
                ReportOnce(
                    $"department:{tenantId:N}:{project.DepartmentId.Value:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the Project's same-tenant Department is missing or inactive. TenantId={TenantId} ActionId={ActionId} DepartmentId={DepartmentId}",
                    tenantId,
                    actionId,
                    project.DepartmentId.Value);
                return OrganizationResolution.Invalid;
            }

            organizationId = await _context.Set<BusinessUnit>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.Id == department.BusinessUnitId
                    && item.TenantId == tenantId
                    && item.IsActive
                    && !item.IsDeleted)
                .Select(item => (Guid?)item.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!organizationId.HasValue)
            {
                ReportOnce(
                    $"business-unit:{tenantId:N}:{department.BusinessUnitId:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the Department's same-tenant Business Unit is missing or inactive. TenantId={TenantId} ActionId={ActionId} BusinessUnitId={BusinessUnitId}",
                    tenantId,
                    actionId,
                    department.BusinessUnitId);
                return OrganizationResolution.Invalid;
            }

            var departmentOrganizationIsActive = await _context.Set<Organization>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(item => item.Id == organizationId.Value
                    && item.TenantId == tenantId
                    && item.IsActive
                    && !item.IsDeleted,
                    cancellationToken);
            if (!departmentOrganizationIsActive)
            {
                ReportOnce(
                    $"organization:{tenantId:N}:{organizationId.Value:N}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up is deferred because the Department's Organization is missing, inactive, or outside its Tenant. TenantId={TenantId} ActionId={ActionId} OrganizationId={OrganizationId}",
                    tenantId,
                    actionId,
                    organizationId.Value);
                return OrganizationResolution.Invalid;
            }

            return OrganizationResolution.Valid(organizationId);
        }

        return OrganizationResolution.ValidWithoutOrganization;
    }

    private async Task<IReadOnlyList<Guid>> ResolveCurrentRecipientsAsync(CurrentAction current, CancellationToken cancellationToken)
    {
        var directOwnerUserIds = new[] { current.Action.OwnerUserId, current.Action.Risk.OwnerUserId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var legacyOwnerResourceIds = new[]
            {
                current.Action.OwnerUserId.HasValue ? null : current.Action.OwnerResourceId,
                current.Action.Risk.OwnerUserId.HasValue ? null : current.Action.Risk.OwnerResourceId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var legacyLinkedUserIds = legacyOwnerResourceIds.Length == 0
            ? new List<Guid>()
            : await _context.Set<Resource>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(resource => legacyOwnerResourceIds.Contains(resource.Id)
                    && resource.TenantId == current.TenantId
                    && !resource.IsDeleted
                    && resource.IsActive
                    && resource.UserId.HasValue)
                .Select(resource => resource.UserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        var candidateUserIds = directOwnerUserIds.Concat(legacyLinkedUserIds).Distinct().ToArray();
        if (candidateUserIds.Length == 0) return Array.Empty<Guid>();

        return await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => candidateUserIds.Contains(user.Id)
                && user.TenantId == current.TenantId
                && user.IsActive
                && !user.IsDeleted)
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<ResolvedSchedule?> ResolveScheduleAsync(CurrentAction current, CancellationToken cancellationToken)
    {
        RegionalSettings settings;
        try
        {
            settings = await ReadEffectiveRegionalSettingsAsync(
                current.TenantId,
                current.OrganizationId,
                cancellationToken);
        }
        catch (Exception exception) when (exception is SettingsPersistenceUnavailableException
                                          or SettingsOwnerNotFoundException
                                          or InvalidOperationException
                                          or KeyNotFoundException)
        {
            ReportOnce(
                $"settings:{current.TenantId:N}:{current.OrganizationId?.ToString("N") ?? "tenant"}:{exception.GetType().FullName}",
                LogLevel.Warning,
                exception,
                "Risk Action follow-up is deferred because effective Organization regional settings could not be resolved. TenantId={TenantId} OrganizationId={OrganizationId} ErrorType={ErrorType} Detail={Detail}",
                current.TenantId,
                current.OrganizationId,
                exception.GetType().Name,
                exception.Message);
            return null;
        }

        var calendar = await ReadEffectiveCalendarAsync(current.Project, current.OrganizationId, current.TenantId, settings.WorkingCalendarId, cancellationToken);
        var followUpDate = DateOnly.FromDateTime(current.Action.NextFollowUpDate!.Value);
        var result = RiskActionFollowUpScheduleResolver.Resolve(followUpDate, calendar, settings.TimeZoneId);
        if (result is RiskActionFollowUpScheduleResult.Invalid invalid)
        {
            var calendarKey = current.Project.WorkingCalendarId?.ToString("N") ?? settings.WorkingCalendarId?.ToString("N") ?? "unconfigured";
            ReportOnce(
                $"calendar:{current.TenantId:N}:{calendarKey}:{invalid.Reason}",
                LogLevel.Warning,
                null,
                "Risk Action follow-up remains pending because its effective Working Calendar cannot produce a due time. TenantId={TenantId} ActionId={ActionId} ProjectId={ProjectId} WorkingCalendarId={WorkingCalendarId} Reason={Reason}",
                current.TenantId,
                current.Action.Id,
                current.Project.Id,
                current.Project.WorkingCalendarId,
                invalid.Reason);
            return null;
        }

        return result is RiskActionFollowUpScheduleResult.Scheduled scheduled
            ? new ResolvedSchedule(scheduled.DueAtUtc, settings.Language)
            : null;
    }

    private async Task<WorkingCalendar?> ReadEffectiveCalendarAsync(
        ProjectEntity project,
        Guid? organizationId,
        Guid tenantId,
        Guid? organizationCalendarId,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<WorkingCalendar>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(calendar => calendar.DayRules)
            .Include(calendar => calendar.Holidays)
            .Where(calendar => calendar.TenantId == tenantId && !calendar.IsDeleted);

        if (project.WorkingCalendarId.HasValue)
        {
            var selected = await query.FirstOrDefaultAsync(
                calendar => calendar.Id == project.WorkingCalendarId.Value,
                cancellationToken);
            if (selected is null)
            {
                ReportOnce(
                    $"calendar:{tenantId:N}:{project.WorkingCalendarId.Value:N}:{RiskActionFollowUpScheduleInvalidReason.MissingCalendar}",
                    LogLevel.Warning,
                    null,
                    "Risk Action follow-up remains pending because the Project's selected Working Calendar is missing, deleted, or outside its Tenant. TenantId={TenantId} ProjectId={ProjectId} WorkingCalendarId={WorkingCalendarId}",
                    tenantId,
                    project.Id,
                    project.WorkingCalendarId.Value);
            }
            return selected;
        }

        if (!organizationId.HasValue || !organizationCalendarId.HasValue)
        {
            ReportOnce(
                $"calendar:{tenantId:N}:{organizationId?.ToString("N") ?? "no-organization"}:unconfigured:{RiskActionFollowUpScheduleInvalidReason.MissingCalendar}",
                LogLevel.Warning,
                null,
                "Risk Action follow-up remains pending because neither the Project nor its effective Organization has a configured Working Calendar. TenantId={TenantId} ProjectId={ProjectId} OrganizationId={OrganizationId}",
                tenantId,
                project.Id,
                organizationId);
            return null;
        }

        var organizationCalendar = await query.FirstOrDefaultAsync(calendar => calendar.Id == organizationCalendarId.Value, cancellationToken);
        if (organizationCalendar is null)
        {
            ReportOnce(
                $"calendar:{tenantId:N}:{organizationCalendarId.Value:N}:{RiskActionFollowUpScheduleInvalidReason.MissingCalendar}",
                LogLevel.Warning,
                null,
                "Risk Action follow-up remains pending because the effective Organization Working Calendar is missing, deleted, or outside its Tenant. TenantId={TenantId} ProjectId={ProjectId} OrganizationId={OrganizationId} WorkingCalendarId={WorkingCalendarId}",
                tenantId,
                project.Id,
                organizationId,
                organizationCalendarId.Value);
        }
        return organizationCalendar;
    }

    private async Task<RegionalSettings> ReadEffectiveRegionalSettingsAsync(
        Guid tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (!_settingsRepository.IsAvailable)
            throw new SettingsPersistenceUnavailableException("The settings repository is not available.");

        var platform = await _settingsRepository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        var tenant = await _settingsRepository.ReadAsync(SettingsScopeIdentity.Tenant(tenantId), cancellationToken);
        var values = organizationId.HasValue
            ? _configurationResolver.ResolveOrganization(
                _settingCatalog,
                platform,
                tenant,
                await _settingsRepository.ReadAsync(SettingsScopeIdentity.Organization(tenantId, organizationId.Value), cancellationToken))
            : _configurationResolver.ResolveTenant(_settingCatalog, platform, tenant);
        var byKey = values.ToDictionary(item => item.Definition.Key, item => item.Value, StringComparer.Ordinal);

        if (!byKey.TryGetValue(TimeZoneSettingKey, out var timeZoneValue)
            || timeZoneValue.ValueKind != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(timeZoneValue.GetString()))
            throw new InvalidOperationException("Effective regional.defaultTimeZone is missing or invalid.");
        if (!byKey.TryGetValue(LanguageSettingKey, out var languageValue)
            || languageValue.ValueKind != System.Text.Json.JsonValueKind.String)
            throw new InvalidOperationException("Effective regional.defaultLanguage is missing or invalid.");

        var language = languageValue.GetString();
        if (language is not ("en" or "ar"))
            throw new InvalidOperationException("Effective regional.defaultLanguage must be en or ar.");

        var timeZoneId = timeZoneValue.GetString()!;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException
                                          or InvalidTimeZoneException
                                          or ArgumentException)
        {
            throw new InvalidOperationException("Effective regional.defaultTimeZone is not recognized.", exception);
        }

        Guid? workingCalendarId = null;
        if (byKey.TryGetValue(WorkingCalendarSettingKey, out var calendarValue)
            && calendarValue.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            if (calendarValue.ValueKind != System.Text.Json.JsonValueKind.String
                || !Guid.TryParse(calendarValue.GetString(), out var parsedCalendarId)
                || parsedCalendarId == Guid.Empty)
                throw new InvalidOperationException("Effective workingCalendar.defaultCalendarId is invalid.");
            workingCalendarId = parsedCalendarId;
        }

        return new RegionalSettings(timeZoneId, language, workingCalendarId);
    }

    private Notification CreateNotification(
        CurrentAction current,
        DateTimeOffset dueAtUtc,
        Guid recipientId,
        NotificationChannelEligibility channels,
        string language,
        DateTimeOffset sentAtUtc)
    {
        var isArabic = string.Equals(language, "ar", StringComparison.Ordinal);
        var actionTitle = current.Action.Title;
        var projectName = current.Project.Name;
        var riskTitle = current.Action.Risk.Title;
        var title = isArabic
            ? "\u062d\u0627\u0646 \u0645\u0648\u0639\u062f \u0645\u062a\u0627\u0628\u0639\u0629 \u0625\u062c\u0631\u0627\u0621 \u0627\u0644\u062e\u0637\u0631"
            : "Risk Action Follow-up Due";
        var message = isArabic
            ? $"\u062d\u0627\u0646 \u0645\u0648\u0639\u062f \u0627\u0644\u0645\u062a\u0627\u0628\u0639\u0629 \u0627\u0644\u0645\u062d\u062f\u062f \u0644\u0644\u0625\u062c\u0631\u0627\u0621 \u00ab{actionTitle}\u00bb. \u064a\u0631\u062c\u0649 \u0645\u0631\u0627\u062c\u0639\u0629 \u0627\u0644\u0625\u062c\u0631\u0627\u0621 \u0648\u062a\u062d\u062f\u064a\u062b \u062d\u0627\u0644\u0629 \u0627\u0644\u062a\u0646\u0641\u064a\u0630.\n\u0627\u0644\u0645\u0634\u0631\u0648\u0639: {projectName}\n\u0627\u0644\u062e\u0637\u0631: {riskTitle}"
            : $"The scheduled follow-up for risk action \"{actionTitle}\" is due. Please review the action and update its progress.\nProject: {projectName}\nRisk: {riskTitle}";

        return new Notification
        {
            Id = CreateStableNotificationId(current.TenantId, current.Action.Id, DateOnly.FromDateTime(current.Action.NextFollowUpDate!.Value), recipientId),
            TenantId = current.TenantId,
            OrganizationId = current.OrganizationId,
            UserId = recipientId,
            Type = NotificationTypes.RiskActionFollowUpDue,
            ReferenceType = NotificationReferenceType,
            ReferenceId = current.Action.PublicId,
            IsInAppVisible = channels.InAppEnabled,
            Title = title,
            Body = message,
            ScheduledAt = dueAtUtc.UtcDateTime,
            SentAt = sentAtUtc.UtcDateTime,
            CreatedBy = WorkerActor
        };
    }

    private static Guid CreateStableNotificationId(Guid tenantId, Guid actionId, DateOnly followUpDate, Guid recipientId)
    {
        var occurrence = string.Join('|',
            tenantId.ToString("N"),
            "RiskActionFollowUpDue",
            actionId.ToString("N"),
            followUpDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            recipientId.ToString("N"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(occurrence));
        return new Guid(hash.AsSpan(0, 16));
    }

    private void ReportOnce(
        string key,
        LogLevel level,
        Exception? exception,
        string message,
        params object?[] values)
    {
        if (!ReportedDiagnostics.TryAdd(key, 0)) return;
        if (exception is null)
            _logger.Log(level, message, values);
        else
            _logger.Log(level, exception, message, values);
    }

    private sealed record CandidateReference(Guid TenantId, Guid ActionId);

    private sealed record CurrentAction(Guid TenantId, RiskAction Action, ProjectEntity Project, Guid? OrganizationId);

    private sealed record RegionalSettings(string TimeZoneId, string Language, Guid? WorkingCalendarId);
    private sealed record ResolvedSchedule(DateTimeOffset DueAtUtc, string Language);

    private sealed record OrganizationResolution(bool IsValid, Guid? OrganizationId)
    {
        public static OrganizationResolution Invalid { get; } = new(false, null);
        public static OrganizationResolution ValidWithoutOrganization { get; } = new(true, null);
        public static OrganizationResolution Valid(Guid? organizationId) => new(true, organizationId);
    }
}
