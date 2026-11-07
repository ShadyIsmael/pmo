namespace NEXUS.SharedKernel;

/// <summary>
/// Carries an explicitly selected tenant for trusted server-side work that runs
/// without an HTTP request, such as a tenant-scoped seed or initialization job.
/// Request tenant identity always takes precedence and must come from an authenticated claim.
/// </summary>
public static class TrustedTenantExecutionContext
{
    private static readonly AsyncLocal<Guid?> TenantIdSlot = new();

    public static Guid? CurrentTenantId => TenantIdSlot.Value;

    public static IDisposable Enter(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A non-empty tenant ID is required for trusted execution.", nameof(tenantId));

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
