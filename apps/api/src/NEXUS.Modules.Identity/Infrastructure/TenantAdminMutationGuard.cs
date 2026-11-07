using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Infrastructure;

/// <summary>Serializes tenant access changes and keeps an effective administrator available.</summary>
public sealed class TenantAdminMutationGuard : IAsyncDisposable
{
    private readonly DbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _completed;

    private TenantAdminMutationGuard(DbContext context, IDbContextTransaction? transaction)
    {
        _context = context;
        _transaction = transaction;
    }

    public static async Task<TenantAdminMutationGuard> AcquireAsync(
        DbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required for an administration mutation.", nameof(tenantId));

        if (!context.Database.IsRelational())
            return new TenantAdminMutationGuard(context, null);

        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
                await AcquireSqlServerApplicationLockAsync(context, transaction, tenantId, cancellationToken);

            return new TenantAdminMutationGuard(context, transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> HasEffectiveTenantAdminAsync(
        IUserEffectivePermissionService effectivePermissions,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var activeUserIds = await _context.Set<NEXUS.Modules.Identity.Domain.User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in activeUserIds)
        {
            var permissions = await effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            if (ProtectedTenantAdminPolicy.IsEffectiveTenantAdmin(permissions))
                return true;
        }

        return false;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            await _transaction.CommitAsync(cancellationToken);
        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            if (!_completed)
                await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    private static async Task AcquireSqlServerApplicationLockAsync(
        DbContext context,
        IDbContextTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @result;";
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.DbType = DbType.String;
        resource.Value = $"NEXUS:TenantAdminMutation:{tenantId:N}";
        command.Parameters.Add(resource);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new InvalidOperationException($"Could not acquire the tenant administration mutation lock (SQL Server result {result}).");
    }
}
