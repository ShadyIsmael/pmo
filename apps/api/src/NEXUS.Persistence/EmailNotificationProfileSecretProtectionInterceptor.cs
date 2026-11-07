using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Persistence;

/// <summary>Protects SMTP credentials at the EF persistence boundary.</summary>
public sealed class EmailNotificationProfileSecretProtectionInterceptor(IIntegrationSecretProtector secrets) : SaveChangesInterceptor
{
    private readonly IIntegrationSecretProtector _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProtectTrackedSecrets(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProtectTrackedSecrets(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void ProtectTrackedSecrets(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries<EmailNotificationProfileEntity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.ProtectedPassword = _secrets.Protect(entry.Entity.ProtectedPassword);
        }
    }
}
