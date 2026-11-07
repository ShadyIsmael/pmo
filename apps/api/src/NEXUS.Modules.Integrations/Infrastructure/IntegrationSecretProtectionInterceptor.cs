using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Infrastructure;

/// <summary>
/// Protects integration credentials at the EF persistence boundary so new and modified
/// IntegrationConnection and IntegrationWebhook records cannot be saved with plaintext.
/// </summary>
public sealed class IntegrationSecretProtectionInterceptor(IIntegrationSecretProtector secrets) : SaveChangesInterceptor
{
    private readonly IIntegrationSecretProtector _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
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

        foreach (var entry in context.ChangeTracker.Entries<IntegrationConnection>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            // Re-protect all fields on modified rows as well. This opportunistically
            // migrates a legacy value when its row is next edited, while unchanged rows
            // still require the explicit P0-07C data migration.
            entry.Entity.ApiKey = _secrets.Protect(entry.Entity.ApiKey);
            entry.Entity.ApiSecret = _secrets.Protect(entry.Entity.ApiSecret);
        }

        foreach (var entry in context.ChangeTracker.Entries<IntegrationWebhook>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.Secret = _secrets.Protect(entry.Entity.Secret);
        }
    }
}