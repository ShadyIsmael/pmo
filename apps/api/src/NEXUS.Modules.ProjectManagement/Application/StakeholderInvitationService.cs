using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class StakeholderInvitationService : IStakeholderInvitationService
{
    private readonly DbContext _context;
    private readonly IResourceReferenceLookup _resources;
    private readonly IResourceIdentityLinker _resourceLinker;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public StakeholderInvitationService(
        DbContext context,
        IResourceReferenceLookup resources,
        IResourceIdentityLinker resourceLinker,
        IPasswordHasher<User> passwordHasher,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _resources = resources;
        _resourceLinker = resourceLinker;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<Result<StakeholderInvitationDto>> SendAsync(Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default)
    {
        var eligible = await FindEligibleStakeholderAsync(projectId, stakeholderId, cancellationToken);
        if (eligible is null)
            return Result<StakeholderInvitationDto>.Failure(Error.Validation("Only an active stakeholder Resource without a system user and with an email can be invited"));

        var (project, stakeholder, resource) = eligible.Value;
        var existing = await _context.Set<StakeholderInvitation>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.ResourceId == resource.Id && item.Status == StakeholderInvitationStatus.Pending, cancellationToken);
        if (existing is not null)
        {
            existing.Status = StakeholderInvitationStatus.Revoked;
            existing.RevokedAt = DateTime.UtcNow;
            existing.UpdatedBy = actorId;
        }

        var rawToken = CreateToken();
        var invitation = new StakeholderInvitation
        {
            TenantId = project.TenantId,
            ResourceId = resource.Id,
            ProjectId = project.Id,
            ProjectStakeholderId = stakeholder.Id,
            Email = resource.Email!,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddHours(GetExpiryHours()),
            Status = StakeholderInvitationStatus.Pending,
            RowVersion = RandomNumberGenerator.GetBytes(8),
            CreatedBy = actorId
        };
        _context.Set<StakeholderInvitation>().Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<StakeholderInvitationDto>.Success(ToDto(invitation, rawToken));
    }

    public async Task<Result<StakeholderInvitationDto>> GetAsync(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken = default)
    {
        var invitation = await _context.Set<StakeholderInvitation>()
            .Where(item => item.ProjectId == projectId && item.ProjectStakeholderId == stakeholderId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (invitation is null) return Result<StakeholderInvitationDto>.Failure(Error.NotFound("Invitation not found"));
        await ExpireIfNeededAsync(invitation, cancellationToken);
        return Result<StakeholderInvitationDto>.Success(ToDto(invitation));
    }

    public async Task<Result<StakeholderInvitationDto>> RevokeAsync(Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default)
    {
        var invitation = await _context.Set<StakeholderInvitation>()
            .Where(item => item.ProjectId == projectId && item.ProjectStakeholderId == stakeholderId)
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

    public async Task<Result<StakeholderInvitationValidationDto>> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await LoadByTokenAsync(token, cancellationToken);
        var error = await ValidateInvitationStateAsync(invitation, cancellationToken);
        if (error is not null) return Result<StakeholderInvitationValidationDto>.Failure(error);
        if (invitation is null) return Result<StakeholderInvitationValidationDto>.Failure(Error.NotFound("Invitation is invalid or no longer available"));

        var project = await _context.Set<Project>().IgnoreQueryFilters().AsNoTracking().FirstAsync(item => item.Id == invitation!.ProjectId, cancellationToken);
        var resource = await _context.Set<NEXUS.Modules.ResourceManagement.Domain.Resource>().IgnoreQueryFilters().AsNoTracking().FirstAsync(item => item.Id == invitation.ResourceId, cancellationToken);
        return Result<StakeholderInvitationValidationDto>.Success(new StakeholderInvitationValidationDto
        {
            ProjectId = project.Id,
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            Email = resource.Email!,
            ProjectName = project.Name,
            RelationshipType = ProjectRelationshipType.Stakeholder,
            ExpiresAt = invitation!.ExpiresAt
        });
    }

    public async Task<Result<StakeholderInvitationAcceptanceDto>> AcceptAsync(string token, AcceptStakeholderInvitationDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Validation("Password must be at least 6 characters"));
        if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Validation("Passwords do not match"));

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var invitation = await LoadByTokenAsync(token, cancellationToken);
        var error = await ValidateInvitationStateAsync(invitation, cancellationToken);
        if (error is not null) return Result<StakeholderInvitationAcceptanceDto>.Failure(error);

        var resource = await _context.Set<NEXUS.Modules.ResourceManagement.Domain.Resource>()
            .IgnoreQueryFilters()
            .FirstAsync(item => item.Id == invitation!.ResourceId, cancellationToken);
        var tenantId = invitation!.TenantId;
        var email = resource.Email!.Trim();
        var users = await _context.Set<User>().IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && !user.IsDeleted && user.Email == email)
            .ToListAsync(cancellationToken);
        if (users.Count > 1)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Conflict("More than one account matches this Resource email; contact an administrator"));

        User user;
        if (users.Count == 1)
        {
            user = users[0];
            if (!user.IsActive)
                return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Conflict("The matching account is inactive; contact an administrator"));
        }
        else
        {
            var (firstName, lastName) = SplitName(resource.Name, email);
            user = new User
            {
                TenantId = tenantId,
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(new User(), dto.Password),
                FirstName = firstName,
                LastName = lastName,
                JobTitle = resource.JobTitle,
                Phone = resource.Phone,
                AvatarUrl = resource.AvatarUrl,
                IsActive = true,
                CreatedBy = "StakeholderInvitation"
            };
            _context.Set<User>().Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (resource.UserId.HasValue && resource.UserId.Value != user.Id)
            return Result<StakeholderInvitationAcceptanceDto>.Failure(Error.Conflict("The Resource is already linked to another system user"));
        var link = await _resourceLinker.LinkUserAsync(resource.Id, user.Id, user.Id.ToString(), cancellationToken);
        if (link.IsFailure) return Result<StakeholderInvitationAcceptanceDto>.Failure(link.Error!);

        invitation.Status = StakeholderInvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.UpdatedBy = user.Id.ToString();
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StakeholderInvitationAcceptanceDto>.Success(new StakeholderInvitationAcceptanceDto
        {
            UserId = user.Id,
            ResourceId = resource.Id,
            Email = user.Email,
            Status = invitation.Status.ToString()
        });
    }

    private async Task<(Project Project, ProjectStakeholder Stakeholder, ResourceReference Resource)?> FindEligibleStakeholderAsync(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken)
    {
        var stakeholder = await _context.Set<ProjectStakeholder>().FirstOrDefaultAsync(item =>
            item.Id == stakeholderId && item.ProjectId == projectId && !item.IsDeleted &&
            item.Status == ProjectStakeholderStatus.Active && item.RelationshipType == ProjectRelationshipType.Stakeholder,
            cancellationToken);
        if (stakeholder is null || !stakeholder.ResourceId.HasValue) return null;
        var project = await _context.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, cancellationToken);
        var resource = await _resources.FindAsync(stakeholder.ResourceId.Value, cancellationToken);
        if (project is null || resource is null || resource.UserId.HasValue || !resource.IsActive || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(resource.Email))
            return null;
        return (project, stakeholder, resource);
    }

    private async Task<StakeholderInvitation?> LoadByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
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

        var project = await _context.Set<Project>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item => item.Id == invitation.ProjectId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        var stakeholder = await _context.Set<ProjectStakeholder>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item => item.Id == invitation.ProjectStakeholderId && item.ProjectId == invitation.ProjectId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        var resource = await _context.Set<NEXUS.Modules.ResourceManagement.Domain.Resource>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item => item.Id == invitation.ResourceId && item.TenantId == invitation.TenantId && !item.IsDeleted, cancellationToken);
        if (project is null || stakeholder is null || resource is null || stakeholder.Status != ProjectStakeholderStatus.Active || stakeholder.RelationshipType != ProjectRelationshipType.Stakeholder || stakeholder.ResourceId != resource.Id || !resource.IsActive || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase) || resource.UserId.HasValue)
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

    private StakeholderInvitationDto ToDto(StakeholderInvitation invitation, string? rawToken = null)
    {
        var url = string.Equals(_environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase) && rawToken is not null ? BuildInvitationUrl(rawToken) : null;
        return new StakeholderInvitationDto
        {
            Id = invitation.Id,
            ProjectId = invitation.ProjectId,
            ProjectStakeholderId = invitation.ProjectStakeholderId,
            ResourceId = invitation.ResourceId,
            Email = invitation.Email,
            Status = invitation.Status.ToString(),
            ExpiresAt = invitation.ExpiresAt,
            AcceptedAt = invitation.AcceptedAt,
            RevokedAt = invitation.RevokedAt,
            InvitationUrl = url,
            EmailDeliveryDeferred = true
        };
    }

    private string BuildInvitationUrl(string token)
    {
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4000";
        return $"{baseUrl}/en/stakeholder/accept-invitation?token={Uri.EscapeDataString(token)}";
    }

    private int GetExpiryHours() => int.TryParse(_configuration["StakeholderInvitations:ExpiryHours"], out var value) && value > 0 ? value : 72;
    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static (string FirstName, string LastName) SplitName(string name, string email)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return (email.Split('@')[0], "Stakeholder");
        return (parts[0], parts.Length == 1 ? "Stakeholder" : string.Join(' ', parts.Skip(1)));
    }
}
