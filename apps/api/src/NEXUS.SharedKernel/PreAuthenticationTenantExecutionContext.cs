namespace NEXUS.SharedKernel;

/// <summary>
/// Carries a server-derived tenant for a narrowly scoped anonymous authentication
/// operation after a credential or refresh token has been verified. Never populate
/// this context from a request-supplied tenant identifier alone.
/// </summary>
public static class PreAuthenticationTenantExecutionContext
{
    private static readonly AsyncLocal<Guid?> TenantIdSlot = new();

    public static Guid? CurrentTenantId => TenantIdSlot.Value;

    public static IDisposable EnterAfterCredentialValidation(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A verified non-empty tenant ID is required.", nameof(tenantId));

        var previousTenantId = TenantIdSlot.Value;
        TenantIdSlot.Value = tenantId;
        return new Scope(previousTenantId);
    }

    private sealed class Scope(Guid? previousTenantId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            TenantIdSlot.Value = previousTenantId;
            _disposed = true;
        }
    }
}
