using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Development;

/// <summary>
/// Local Development-only Admin temporary-password recovery. This service is
/// called only by the explicit console operation; it is not registered as an API
/// endpoint or exposed through normal user administration.
/// </summary>
public sealed class DevelopmentAdminPasswordRecoveryService
{
    private readonly DbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly LocalPasswordPolicyOptions _passwordPolicy;
    private readonly IHostEnvironment _environment;

    public DevelopmentAdminPasswordRecoveryService(
        DbContext context,
        IPasswordHasher<User> passwordHasher,
        IOptions<LocalPasswordPolicyOptions> passwordPolicy,
        IHostEnvironment environment)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy.Value;
        _environment = environment;
    }

    public async Task<DateTime> RecoverTemporaryPasswordAsync(
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            throw new InvalidOperationException("Development Admin password recovery is available only in the Development environment.");
        ValidatePasswordLength(temporaryPassword, "temporary");
        if (_passwordPolicy.TemporaryPasswordExpiryHours is < 1 or > 168)
            throw new InvalidOperationException("The configured temporary password expiry must be between 1 and 168 hours.");

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var admin = await _context.Set<User>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.Id == SeededDevelopmentAdmin.UserId
                && user.TenantId == SeededDevelopmentAdmin.TenantId
                && !user.IsDeleted, cancellationToken);
        if (admin is null)
            throw new InvalidOperationException("The canonical Development seeded Admin account was not found.");
        if (!string.Equals(admin.Email, SeededDevelopmentAdmin.Email, StringComparison.Ordinal))
            throw new InvalidOperationException("The canonical Development seeded Admin email does not match the expected account.");
        if (!admin.IsActive || admin.AuthenticationSource != AuthenticationSource.Local)
            throw new InvalidOperationException("The Development tenant Admin account is inactive or does not use local password authentication.");

        var now = DateTime.UtcNow;
        var expiry = now.AddHours(_passwordPolicy.TemporaryPasswordExpiryHours);
        var previousState = JsonSerializer.Serialize(new
        {
            PasswordState = admin.MustChangePassword ? "Temporary" : "Permanent",
            admin.MustChangePassword,
            admin.TemporaryPasswordExpiresAt
        });

        admin.PasswordHash = _passwordHasher.HashPassword(admin, temporaryPassword);
        admin.MustChangePassword = true;
        admin.TemporaryPasswordExpiresAt = expiry;
        admin.PasswordChangedAt = now;
        admin.UpdatedAt = now;
        admin.UpdatedBy = "Development Admin password recovery";

        var sessions = await _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .Where(session => session.UserId == admin.Id
                && session.TenantId == admin.TenantId
                && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAt = now;

        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = admin.TenantId,
            EntityType = "User",
            EntityId = admin.Id,
            Action = "DevelopmentAdminTemporaryPasswordRecovered",
            Changes = "{\"PasswordState\":\"Temporary\",\"MustChangePassword\":true}",
            OriginalValues = previousState,
            NewValues = JsonSerializer.Serialize(new
            {
                PasswordState = "Temporary",
                MustChangePassword = true,
                ExpiresAt = expiry
            }),
            ChangedFields = "PasswordState,MustChangePassword,TemporaryPasswordExpiresAt",
            Description = "Development Admin temporary password was recovered through the local Development-only command.",
            Source = "System",
            UserId = admin.Id,
            UserName = admin.FullName,
            CreatedBy = "Development Admin password recovery"
        });

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return expiry;
    }

    private void ValidatePasswordLength(string password, string kind)
    {
        if (password.Length < _passwordPolicy.MinimumLength || password.Length > _passwordPolicy.MaximumLength)
            throw new InvalidOperationException($"The {kind} password must be between {_passwordPolicy.MinimumLength} and {_passwordPolicy.MaximumLength} characters.");
    }
}
