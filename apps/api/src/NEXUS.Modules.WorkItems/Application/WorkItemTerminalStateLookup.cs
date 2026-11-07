using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Application;

/// <summary>
/// Derives the terminal Work Item status codes of the current tenant from the
/// published workflow definitions whose <c>EntityType</c> is
/// <see cref="WorkItemWorkflow.EntityType"/>, so filters, insights and project
/// overviews use published <c>IsFinal</c> state definitions instead of
/// hardcoded status strings. Read-only: this lookup never creates, rewrites or
/// re-publishes workflow versions.
/// </summary>
public static class WorkItemTerminalStateLookup
{
    /// <summary>
    /// Legacy terminal status set ({ "Done", "Cancelled" }) used when no published
    /// WorkItem workflow of the tenant exposes a final state. Compatibility
    /// versions generated before P0-03-12 marked every state
    /// <c>IsFinal = false</c>, and re-publishing them is an explicit Product
    /// Owner decision, not a runtime action; without this fallback those tenants'
    /// overdue/open/overview metrics would silently flip (every item would count
    /// as open). Falling back preserves the pre-remediation numbers exactly.
    /// </summary>
    public static readonly string[] LegacyFallbackTerminalStatusCodes = { "Done", "Cancelled" };

    /// <summary>
    /// Returns the union of <c>IsActive &amp;&amp; IsFinal</c> state codes from the
    /// active published version (<c>ActivePublishedVersionId</c>) of every
    /// non-deleted workflow definition of <paramref name="tenantId"/> whose
    /// <c>EntityType</c> is <see cref="WorkItemWorkflow.EntityType"/> — covering
    /// both <c>WORKITEM_STANDARD</c> and custom project-selected workflows.
    /// The whole filter is expressed as an EF-translatable query; only the
    /// matching (typically handful) of state codes is materialized.
    /// </summary>
    public static async Task<string[]> GetAsync(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId(httpContextAccessor);
        if (tenantId is null)
        {
            // No authenticated tenant claim and no trusted tenant execution
            // context: the tenant's published workflow state cannot be
            // identified, so preserve the legacy terminal semantics rather than
            // failing a read or reporting every item as open.
            return LegacyFallbackTerminalStatusCodes;
        }

        var tenant = tenantId.Value;
        var terminalCodes = await (
            from definition in context.Set<WorkflowDefinition>()
            where definition.TenantId == tenant
                && definition.EntityType == WorkItemWorkflow.EntityType
                && !definition.IsDeleted
                && definition.ActivePublishedVersionId != null
            from version in context.Set<WorkflowDefinitionVersion>()
            where version.Id == definition.ActivePublishedVersionId
                && version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published
                && !version.IsDeleted
            from state in context.Set<WorkflowState>()
            where state.WorkflowDefinitionVersionId == version.Id
                && state.IsActive
                && state.IsFinal
                && !state.IsDeleted
            select state.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return terminalCodes.Count > 0 ? terminalCodes.ToArray() : LegacyFallbackTerminalStatusCodes;
    }

    private static Guid? ResolveTenantId(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirst("tenantId")?.Value, out var claimTenantId)
            && claimTenantId != Guid.Empty)
            return claimTenantId;

        return TrustedTenantExecutionContext.CurrentTenantId is { } trustedTenantId && trustedTenantId != Guid.Empty
            ? trustedTenantId
            : null;
    }
}
