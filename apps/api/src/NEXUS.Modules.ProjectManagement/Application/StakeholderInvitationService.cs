using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Project-scoped invitation and consent for external Resources. The one-time token
/// can only record accept/decline; this service never creates Users or issues sessions.
/// </summary>
public sealed class StakeholderInvitationService : IStakeholderInvitationService
{
    private const string ConsentVersion = "project-stakeholder-2026-10-v1";
    private const int DefaultExpiryHours = 72;
    private const int MaximumExpiryHours = 168;

    private readonly DbContext _context;
    private readonly IResourceReferenceLookup _resources;
    private readonly IEmailNotificationSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StakeholderInvitationService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public StakeholderInvitationService(
        DbContext context,
        IResourceReferenceLookup resources,
        IEmailNotificationSender emailSender,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<StakeholderInvitationService> logger,
        IHttpContextAccessor? httpContextAccessor = null,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _resources = resources;
        _emailSender = emailSender;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<Result<StakeholderInvitationDto>> SendAsync(
        Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireManagementAccess(projectId, actorId, mutation: true);
        if (accessError is not null) return Result<StakeholderInvitationDto>.Failure(accessError);
        var eligible = await FindEligibleStakeholderAsync(projectId, stakeholderId, cancellationToken);
        if (eligible is null)
            return Result<StakeholderInvitationDto>.Failure(Error.Validation(
                "Only an active stakeholder Resource without a system user and with an email can be invited"));

        var (project, stakeholder, resource) = eligible.Value;
        var invitations = _context.Set<StakeholderInvitation>();
        var prior = await invitations
            .Where(item => item.ProjectId == projectId && item.ProjectStakeholderId == stakeholderId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        if (prior.Any(item => item.Status == StakeholderInvitationStatus.Accepted))
            return Result<StakeholderInvitationDto>.Failure(Error.Conflict("This stakeholder has already accepted the project invitation"));
        foreach (var pending in prior.Where(item => item.Status == StakeholderInvitationStatus.Pending))
        {
            pending.Status = StakeholderInvitationStatus.Revoked;
            pending.RevokedAt = DateTime.UtcNow;
            pending.UpdatedBy = actorId;
        }

        var rawToken = CreateToken();
        var invitation = new StakeholderInvitation
        {
            TenantId = project.TenantId,
            ResourceId = resource.Id,
            ProjectId = project.Id,
            ProjectStakeholderId = stakeholder.Id,
            Email = resource.Email!.Trim(),
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddHours(GetExpiryHours()),
            Status = StakeholderInvitationStatus.Pending,
            EmailDeliveryStatus = "Pending",
            RowVersion = RandomNumberGenerator.GetBytes(8),
            CreatedBy = actorId
        };
        invitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        var invitationUrl = TryBuildInvitationUrl(rawToken);
        var delivery = invitationUrl is null
            ? new EmailSendResult(false, EmailFailureKind.Permanent, "FrontendUrlUnavailable", "The invitation link is not configured.")
            : await _emailSender.SendAsync(new Notification
            {
                TenantId = project.TenantId,
                Title = $"Project invitation: {project.Name}",
                Body = BuildInvitationBody(project.Name, invitationUrl, invitation.ExpiresAt),
                Type = "ProjectStakeholderInvitation",
                ReferenceType = "Project",
                ReferenceId = project.Id,
                IsInAppVisible = false,
                CreatedBy = actorId
            }, invitation.Email, cancellationToken);

        invitation.EmailDeliveryStatus = delivery.Succeeded
            ? "Sent"
            : string.Equals(delivery.FailureCategory, "EmailUnavailable", StringComparison.Ordinal)
                ? "NotConfigured" : "Failed";
        invitation.EmailSentAt = delivery.Succeeded ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<StakeholderInvitationDto>.Success(ToDto(
            invitation,
            invitationUrl,
            includeDevelopmentLink: !delivery.Succeeded));
    }

    public async Task<Result<StakeholderInvitationDto>> GetAsync(
        Guid projectId, Guid stakeholderId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireManagementAccess(projectId);
        if (accessError is not null) return Result<StakeholderInvitationDto>.Failure(accessError);
        var invitation = await _context.Set<StakeholderInvitation>()
            .Where(item => item.ProjectId == projectId && item.ProjectStakeholderId == stakeholderId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (invitation is null) return Result<StakeholderInvitationDto>.Failure(Error.NotFound("Invitation not found"));
        await ExpireIfNeededAsync(invitation, cancellationToken);
        return Result<StakeholderInvitationDto>.Success(ToDto(invitation));
    }

    public async Task<Result<StakeholderInvitationDto>> RevokeAsync(
        Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireManagementAccess(projectId, actorId, mutation: true);
        if (accessError is not null) return Result<StakeholderInvitationDto>.Failure(accessError);
        var invitation = await _context.Set<StakeholderInvitation>()
            .Where(item => item.ProjectId == projectId && item.ProjectStakeholderId == stakeholderId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (invitation is null) return Result<StakeholderInvitationDto>.Failure(Error.NotFound("Invitation not found"));
        if (invitation.Status == StakeholderInvitationStatus.Accepted)
            return Result<StakeholderInvitationDto>.Failure(Error.Conflict("An accepted invitation cannot be revoked"));
        if (invitation.Status != StakeholderInvitationStatus.Revoked)
        {
            invitation.Status = StakeholderInvitationStatus.Revoked;
            invitation.RevokedAt = DateTime.UtcNow;
            invitation.UpdatedBy = actorId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Result<StakeholderInvitationDto>.Success(ToDto(invitation));
    }

    public async Task<Result<StakeholderInvitationValidationDto>> ValidateAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var invitation = await LoadByTokenAsync(token, cancellationToken);
        if (invitation is null) return Result<StakeholderInvitationValidationDto>.Failure(Error.NotFound("Invitation is invalid or no longer available"));

        if (invitation.Status == StakeholderInvitationStatus.Accepted)
        {
            return Result<StakeholderInvitationValidationDto>.Success(new StakeholderInvitationValidationDto
            {
                ProjectId = invitation.ProjectId,
                ResourceId = invitation.ResourceId,
                ExpiresAt = invitation.ExpiresAt,
                Status = invitation.Status.ToString(),
                ReportsConsent = invitation.ReportsConsent,
                NotificationsConsent = invitation.NotificationsConsent
            });
        }

        if (invitation.Status == StakeholderInvitationStatus.Pending)
        {
            var error = await ValidateInvitationStateAsync(invitation, cancellationToken);
            if (error is not null) return Result<StakeholderInvitationValidationDto>.Failure(error);
        }
        else
        {
            return Result<StakeholderInvitationValidationDto>.Failure(Error.Conflict($"Invitation is {invitation.Status.ToString().ToLowerInvariant()}"));
        }

        var project = await _context.Set<Project>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation.ProjectId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        var resource = await _context.Set<Resource>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation.ResourceId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        if (project is null || resource is null || !EmailMatches(resource.Email, invitation.Email))
            return Result<StakeholderInvitationValidationDto>.Failure(Error.NotFound("Invitation is invalid or no longer available"));

        return Result<StakeholderInvitationValidationDto>.Success(new StakeholderInvitationValidationDto
        {
            ProjectId = project.Id,
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            Email = invitation.Email,
            ProjectName = project.Name,
            RelationshipType = ProjectRelationshipType.Stakeholder,
            ExpiresAt = invitation.ExpiresAt,
            Status = invitation.Status.ToString(),
            ReportsConsent = invitation.ReportsConsent,
            NotificationsConsent = invitation.NotificationsConsent
        });
    }

    public async Task<Result<StakeholderInvitationAcceptanceDto>> WithdrawConsentAsync(
        string token, WithdrawStakeholderConsentDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.WithdrawReportsConsent && !dto.WithdrawNotificationsConsent)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Validation("Select at least one consent preference to withdraw"));

        var invitation = await LoadByTokenAsync(token, cancellationToken);
        if (invitation is null)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.NotFound("Invitation is invalid or no longer available"));
        if (invitation.Status != StakeholderInvitationStatus.Accepted)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Conflict("Consent can only be withdrawn after invitation acceptance"));

        var changed = false;
        if (dto.WithdrawReportsConsent && invitation.ReportsConsent)
        {
            invitation.ReportsConsent = false;
            changed = true;
        }
        if (dto.WithdrawNotificationsConsent && invitation.NotificationsConsent)
        {
            invitation.NotificationsConsent = false;
            changed = true;
        }

        if (changed)
        {
            invitation.UpdatedBy = "ExternalStakeholderConsentWithdrawal";
            invitation.RowVersion = RandomNumberGenerator.GetBytes(8);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<StakeholderInvitationAcceptanceDto>.Success(new StakeholderInvitationAcceptanceDto
        {
            ResourceId = invitation.ResourceId,
            Email = invitation.Email,
            Status = invitation.Status.ToString(),
            ReportsConsent = invitation.ReportsConsent,
            NotificationsConsent = invitation.NotificationsConsent
        });
    }

    public async Task<Result<StakeholderInvitationAcceptanceDto>> AcceptAsync(
        string token, AcceptStakeholderInvitationDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.AcceptParticipation)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Validation("Confirm that you accept the project stakeholder invitation"));

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var invitation = await LoadByTokenAsync(token, cancellationToken);
        var error = await ValidateInvitationStateAsync(invitation, cancellationToken);
        if (error is not null)
        {
            // Expiry validation persists the Expired status. Commit that state even though
            // the attempted acceptance/decline is rejected.
            await transaction.CommitAsync(cancellationToken);
            return Result<StakeholderInvitationAcceptanceDto>.Failure(error);
        }

        var resource = await _context.Set<Resource>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation!.ResourceId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        if (resource is null || !EmailMatches(resource.Email, invitation!.Email))
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.NotFound("Invitation is invalid or no longer available"));

        invitation.Status = StakeholderInvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.ConsentedAt = invitation.AcceptedAt;
        invitation.ConsentVersion = ConsentVersion;
        invitation.ReportsConsent = dto.ReceiveProjectReports;
        invitation.NotificationsConsent = dto.ReceiveProjectNotifications;
        invitation.UpdatedBy = "ExternalStakeholder";
        invitation.RowVersion = RandomNumberGenerator.GetBytes(8);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StakeholderInvitationAcceptanceDto>.Success(new StakeholderInvitationAcceptanceDto
        {
            ResourceId = resource.Id,
            Email = invitation.Email,
            Status = invitation.Status.ToString(),
            ReportsConsent = invitation.ReportsConsent,
            NotificationsConsent = invitation.NotificationsConsent
        });
    }

    public async Task<Result<StakeholderInvitationAcceptanceDto>> DeclineAsync(
        string token, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var invitation = await LoadByTokenAsync(token, cancellationToken);
        var error = await ValidateInvitationStateAsync(invitation, cancellationToken);
        if (error is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<StakeholderInvitationAcceptanceDto>.Failure(error);
        }

        invitation!.Status = StakeholderInvitationStatus.Declined;
        invitation.DeclinedAt = DateTime.UtcNow;
        invitation.ReportsConsent = false;
        invitation.NotificationsConsent = false;
        invitation.UpdatedBy = "ExternalStakeholder";
        invitation.RowVersion = RandomNumberGenerator.GetBytes(8);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StakeholderInvitationAcceptanceDto>.Success(new StakeholderInvitationAcceptanceDto
        {
            ResourceId = invitation.ResourceId,
            Email = invitation.Email,
            Status = invitation.Status.ToString(),
            ReportsConsent = false,
            NotificationsConsent = false
        });
    }

    public async Task<int> NotifyProjectReportPublishedAsync(
        Guid projectId, Guid reportId, DateTime statusDate, string? summary, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, cancellationToken);
        if (project is null) return 0;

        var invitations = await _context.Set<StakeholderInvitation>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == project.Id
                && item.Status == StakeholderInvitationStatus.Accepted
                && (item.ReportsConsent || item.NotificationsConsent)
                && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (invitations.Count == 0) return 0;

        var activeStakeholderIds = await _context.Set<ProjectStakeholder>().AsNoTracking()
            .Where(stakeholder => stakeholder.ProjectId == project.Id && stakeholder.TenantId == project.TenantId
                && stakeholder.Status == ProjectStakeholderStatus.Active
                && stakeholder.RelationshipType == ProjectRelationshipType.Stakeholder
                && !stakeholder.IsDeleted)
            .Select(stakeholder => stakeholder.Id)
            .ToHashSetAsync(cancellationToken);
        var resourceIds = invitations.Where(item => activeStakeholderIds.Contains(item.ProjectStakeholderId))
            .Select(item => item.ResourceId).Distinct().ToArray();
        var resources = await _context.Set<Resource>().AsNoTracking()
            .Where(resource => resourceIds.Contains(resource.Id) && resource.TenantId == project.TenantId
                && resource.IsActive && !resource.IsDeleted && !resource.UserId.HasValue)
            .ToDictionaryAsync(resource => resource.Id, cancellationToken);

        var sent = 0;
        foreach (var invitation in invitations)
        {
            if (!activeStakeholderIds.Contains(invitation.ProjectStakeholderId)
                || !resources.TryGetValue(invitation.ResourceId, out var resource)
                || !EmailMatches(resource.Email, invitation.Email)) continue;

            var subject = invitation.ReportsConsent
                ? $"Project report: {project.Name}"
                : $"Project update: {project.Name}";
            var excerpt = invitation.ReportsConsent && !string.IsNullOrWhiteSpace(summary)
                ? $"\n\n{summary.Trim()[..Math.Min(summary.Trim().Length, 2000)]}"
                : "";
            var body = $"A status report for project {project.Name} was published for {statusDate:yyyy-MM-dd}."
                + excerpt
                + "\n\nThis update contains information for this project only. It does not provide an account or platform access."
                + "\n\nتم نشر تقرير حالة للمشروع " + project.Name + " بتاريخ " + statusDate.ToString("yyyy-MM-dd")
                + "\nهذه الرسالة تخص هذا المشروع فقط ولا تمنح حساباً أو وصولاً إلى المنصة.";
            var result = await _emailSender.SendAsync(new Notification
            {
                TenantId = project.TenantId,
                Email = invitation.Email,
                Title = subject,
                Body = body,
                Type = "ProjectStatusReport",
                ReferenceType = "ProjectStatusReport",
                ReferenceId = reportId,
                IsInAppVisible = false,
                CreatedBy = "ProjectStatusReport"
            }, invitation.Email, cancellationToken);
            if (result.Succeeded)
            {
                sent++;
                _logger.LogInformation(
                    "Project report email accepted by provider for Tenant {TenantId}, Project {ProjectId}, Report {ReportId}, Stakeholder {StakeholderId}.",
                    project.TenantId, project.Id, reportId, invitation.ProjectStakeholderId);
            }
            else
            {
                _logger.LogWarning(
                    "Project report email was not accepted by provider for Tenant {TenantId}, Project {ProjectId}, Report {ReportId}, Stakeholder {StakeholderId}. FailureKind {FailureKind}; FailureCategory {FailureCategory}.",
                    project.TenantId, project.Id, reportId, invitation.ProjectStakeholderId,
                    result.FailureKind, result.FailureCategory ?? "Unspecified");
            }
        }
        return sent;
    }

    private async Task<(Project Project, ProjectStakeholder Stakeholder, ResourceReference Resource)?> FindEligibleStakeholderAsync(
        Guid projectId, Guid stakeholderId, CancellationToken cancellationToken)
    {
        var stakeholder = await _context.Set<ProjectStakeholder>().FirstOrDefaultAsync(item =>
            item.Id == stakeholderId && item.ProjectId == projectId && !item.IsDeleted
            && item.Status == ProjectStakeholderStatus.Active
            && item.RelationshipType == ProjectRelationshipType.Stakeholder, cancellationToken);
        if (stakeholder is null || !stakeholder.ResourceId.HasValue) return null;
        var project = await _context.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, cancellationToken);
        var resource = await _resources.FindAsync(stakeholder.ResourceId.Value, cancellationToken);
        if (project is null || resource is null || resource.UserId.HasValue || !resource.IsActive
            || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(resource.Email)) return null;
        return (project, stakeholder, resource);
    }

    private Error? RequireManagementAccess(Guid projectId, string? actorId = null, bool mutation = false)
    {
        var principal = _httpContextAccessor?.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Error.Unauthorized("An authenticated user is required.");
        if (!Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId)
            || userId == Guid.Empty)
            return Error.Forbidden("A valid tenant and user identity is required.");
        if (!principal.HasClaim("permission", "Stakeholders.Manage"))
            return Error.Forbidden("The Stakeholders.Manage permission is required.");
        if (actorId is not null && (!Guid.TryParse(actorId, out var requestedActor) || requestedActor != userId))
            return Error.Forbidden("The audit actor must match the authenticated user.");

        var management = _managementContext?.Current;
        if (management is null || management.TenantId != tenantId || management.UserId != userId
            || !management.Scope.ProjectIds.Contains(projectId))
            return Error.Forbidden("The project is outside the current management scope.");
        if (mutation && management.EffectiveReadOnly)
            return Error.Forbidden("Stakeholder invitation changes are not available in read-only management mode.");
        return null;
    }

    private async Task<StakeholderInvitation?> LoadByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return null;
        var hash = HashToken(token.Trim());
        return await _context.Set<StakeholderInvitation>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TokenHash == hash && !item.IsDeleted, cancellationToken);
    }

    private async Task<Error?> ValidateInvitationStateAsync(StakeholderInvitation? invitation, CancellationToken cancellationToken)
    {
        if (invitation is null) return Error.NotFound("Invitation is invalid or no longer available");
        if (invitation.Status != StakeholderInvitationStatus.Pending)
            return Error.Conflict($"Invitation is {invitation.Status.ToString().ToLowerInvariant()}");
        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = StakeholderInvitationStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);
            return Error.Conflict("Invitation has expired");
        }

        var project = await _context.Set<Project>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation.ProjectId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        var stakeholder = await _context.Set<ProjectStakeholder>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation.ProjectStakeholderId && item.ProjectId == invitation.ProjectId
                && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        var resource = await _context.Set<Resource>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invitation.ResourceId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        if (project is null || stakeholder is null || resource is null
            || stakeholder.Status != ProjectStakeholderStatus.Active
            || stakeholder.RelationshipType != ProjectRelationshipType.Stakeholder
            || stakeholder.ResourceId != resource.Id || !resource.IsActive
            || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
            || resource.UserId.HasValue || !EmailMatches(resource.Email, invitation.Email))
            return Error.Conflict("The stakeholder relationship or Resource is no longer eligible");
        return null;
    }

    private async Task ExpireIfNeededAsync(StakeholderInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Status == StakeholderInvitationStatus.Pending && invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = StakeholderInvitationStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private StakeholderInvitationDto ToDto(StakeholderInvitation invitation, string? invitationUrl = null, bool includeDevelopmentLink = false) => new()
    {
        Id = invitation.Id,
        ProjectId = invitation.ProjectId,
        ProjectStakeholderId = invitation.ProjectStakeholderId,
        ResourceId = invitation.ResourceId,
        Email = invitation.Email,
        Status = invitation.Status.ToString(),
        ExpiresAt = invitation.ExpiresAt,
        AcceptedAt = invitation.AcceptedAt,
        DeclinedAt = invitation.DeclinedAt,
        RevokedAt = invitation.RevokedAt,
        ReportsConsent = invitation.ReportsConsent,
        NotificationsConsent = invitation.NotificationsConsent,
        EmailDeliveryStatus = invitation.EmailDeliveryStatus,
        EmailSentAt = invitation.EmailSentAt,
        InvitationUrl = includeDevelopmentLink && IsDevelopment() ? invitationUrl : null,
        EmailDeliveryDeferred = invitation.EmailDeliveryStatus != "Sent"
    };

    private string? TryBuildInvitationUrl(string token)
    {
        var configured = _configuration["Frontend:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(configured) && IsDevelopment()) configured = "http://localhost:4000";
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var baseUri)) return null;
        var allowed = baseUri.Scheme == Uri.UriSchemeHttps
            || (IsDevelopment() && baseUri.Scheme == Uri.UriSchemeHttp && IsLoopback(baseUri.Host));
        if (!allowed || !string.IsNullOrEmpty(baseUri.UserInfo)) return null;
        return $"{baseUri.ToString().TrimEnd('/')}/stakeholder/accept-invitation#token={Uri.EscapeDataString(token)}";
    }

    private string BuildInvitationBody(string projectName, string invitationUrl, DateTime expiresAt) =>
        $"You have been invited as an external stakeholder for project {projectName}.\n"
        + $"This invitation expires at {expiresAt:yyyy-MM-dd HH:mm} UTC. Accept or decline here: {invitationUrl}\n"
        + "Acceptance records your consent for this project only. It does not create a User account, issue a JWT, or provide access to project APIs.\n\n"
        + $"تمت دعوتك كمشارك خارجي في مشروع {projectName}.\n"
        + $"تنتهي الدعوة في {expiresAt:yyyy-MM-dd HH:mm} UTC. يمكنك القبول أو الرفض من هنا: {invitationUrl}\n"
        + "القبول يسجل موافقتك على هذا المشروع فقط. لن يتم إنشاء حساب مستخدم أو منح وصول إلى واجهات المشروع.";

    private int GetExpiryHours()
    {
        var configured = int.TryParse(_configuration["StakeholderInvitations:ExpiryHours"], out var value)
            ? value : DefaultExpiryHours;
        return Math.Clamp(configured, 1, MaximumExpiryHours);
    }

    private bool IsDevelopment() => string.Equals(_environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
    private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    private static bool EmailMatches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
