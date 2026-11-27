using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Documents;

public sealed class TemplateDeletionStorageCleanupProcessor(
    AppDbContext db,
    IStorageProvider defaultStorage,
    IServiceProvider services,
    ILogger<TemplateDeletionStorageCleanupProcessor> logger) : ITemplateDeletionStorageCleanupProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters().AsNoTracking()
            .Where(request => request.StorageCleanupStatus == TemplateDeletionRequest.CleanupStatusPending)
            .OrderBy(request => request.StorageCleanupLastAttemptAt)
            .ThenBy(request => request.RequestedAt)
            .Select(request => new { request.TenantId, request.Id })
            .Take(100)
            .ToListAsync(cancellationToken);

        var completed = 0;
        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ProcessRequestAsync(item.TenantId, item.Id, cancellationToken);
                completed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "Storage cleanup for template deletion request {RequestId} remains queued.", item.Id);
            }
        }
        return completed;
    }

    public async Task ProcessRequestAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || requestId == Guid.Empty)
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == requestId
                && item.Status == TemplateDeletionRequest.StatusApproved
                && item.DeletionType == TemplateDeletionRequest.DeletionTypeHardDelete
                && item.StorageCleanupStatus == TemplateDeletionRequest.CleanupStatusPending,
                cancellationToken);
        if (request is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        request.StorageCleanupLastAttemptAt = DateTime.UtcNow;
        await AcquireStorageReferenceWriteLocksAsync(cancellationToken);
        var keys = ParseKeys(request.StorageCleanupKeysJson);
        if (keys is null)
        {
            request.StorageCleanupError = "The stored cleanup payload could not be read.";
            request.RowVersion = NewConcurrencyToken();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var remaining = new List<TemplateDeletionStorageObject>();
        foreach (var item in keys)
        {
            if (string.IsNullOrWhiteSpace(item.ProviderId) || string.IsNullOrWhiteSpace(item.StorageKey))
                continue;

            // Serializable range locks remain held until the cleanup state commits. A concurrent
            // DocumentVersion insert that shares this key must therefore wait for this decision.
            if (await IsStorageObjectReferencedAsync(item, cancellationToken))
                continue;

            var provider = services.GetKeyedService<IStorageProvider>(item.ProviderId);
            if (provider is null)
            {
                remaining.Add(item);
                request.StorageCleanupError = "The storage provider is not registered; cleanup will retry.";
                break;
            }

            try
            {
                await provider.DeleteAsync(item.StorageKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                remaining.Add(item);
                request.StorageCleanupError = "The storage provider could not delete an object; cleanup will retry.";
                logger.LogWarning(exception,
                    "Storage provider {ProviderId} could not delete an object for template deletion request {RequestId}; cleanup will retry.",
                    item.ProviderId, request.Id);
                break;
            }
        }

        // Objects after a provider failure have not been examined and remain queued as well.
        if (remaining.Count > 0)
        {
            var firstUnprocessed = keys.FindIndex(item =>
                string.Equals(item.ProviderId, remaining[0].ProviderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.StorageKey, remaining[0].StorageKey, StringComparison.Ordinal));
            foreach (var item in keys.Skip(Math.Max(0, firstUnprocessed + 1)))
                if (!remaining.Any(existing => SameObject(existing, item))) remaining.Add(item);
        }

        request.StorageCleanupKeysJson = remaining.Count == 0 ? null : JsonSerializer.Serialize(remaining, JsonOptions);
        request.StorageCleanupStatus = remaining.Count == 0
            ? TemplateDeletionRequest.CleanupStatusCompleted
            : TemplateDeletionRequest.CleanupStatusPending;
        if (remaining.Count == 0) request.StorageCleanupError = null;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = "TemplateDeletionStorageCleanupProcessor";
        request.RowVersion = NewConcurrencyToken();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> IsStorageObjectReferencedAsync(
        TemplateDeletionStorageObject item,
        CancellationToken cancellationToken)
    {
        var matchesDefaultProvider = string.Equals(
            item.ProviderId, defaultStorage.ProviderId, StringComparison.OrdinalIgnoreCase);
        var versions = db.DocumentVersions.IgnoreQueryFilters().AsNoTracking();
        var versionReference = await versions.AnyAsync(version =>
            (version.StorageProviderId == item.ProviderId
                || (version.StorageProviderId == null && matchesDefaultProvider))
            && (version.StorageKey == item.StorageKey || version.StoragePath == item.StorageKey),
            cancellationToken);
        if (versionReference) return true;

        // Legacy Document.StoragePath has no provider column, so retain a matching object
        // conservatively regardless of which provider originally wrote it.
        return await db.Documents.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(document => document.StoragePath == item.StorageKey,
                cancellationToken);
    }

    private async Task AcquireStorageReferenceWriteLocksAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            return;

        // There is no provider/key index in the shared legacy Document/DocumentVersion model.
        // Hold exclusive table locks in the Documents-then-DocumentVersions write order until
        // cleanup state commits so no tenant
        // can insert a row that starts sharing the object after the reference scan.
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Storage cleanup requires an active database transaction.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT TOP (1) [Id] FROM [dbo].[Documents] WITH (TABLOCKX, HOLDLOCK); "
            + "SELECT TOP (1) [Id] FROM [dbo].[DocumentVersions] WITH (TABLOCKX, HOLDLOCK);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        do
        {
            while (await reader.ReadAsync(cancellationToken)) { }
        }
        while (await reader.NextResultAsync(cancellationToken));
    }

    private static List<TemplateDeletionStorageObject>? ParseKeys(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<TemplateDeletionStorageObject>>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool SameObject(TemplateDeletionStorageObject left, TemplateDeletionStorageObject right) =>
        string.Equals(left.ProviderId, right.ProviderId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.StorageKey, right.StorageKey, StringComparison.Ordinal);

    private static byte[] NewConcurrencyToken() => Guid.NewGuid().ToByteArray();

    public sealed record TemplateDeletionStorageObject(string ProviderId, string StorageKey);
}

public sealed class TemplateDeletionStorageCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TemplateDeletionStorageCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ITemplateDeletionStorageCleanupProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The template deletion storage cleanup sweep failed; queued cleanup will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
