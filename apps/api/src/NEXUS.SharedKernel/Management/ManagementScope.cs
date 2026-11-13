using System.Text.Json.Serialization;

namespace NEXUS.SharedKernel.Management;

public sealed record ManagementEntityReference(Guid Id, string Name, string? Code = null);

public sealed record ManagementScope(
    IReadOnlySet<Guid> PortfolioIds,
    IReadOnlySet<Guid> ProgramIds,
    IReadOnlySet<Guid> ProjectIds)
{
    public IReadOnlySet<Guid> OrganizationIds { get; init; } = new HashSet<Guid>();
    public IReadOnlyList<ManagementEntityReference> OrganizationReferences { get; init; } = Array.Empty<ManagementEntityReference>();
    [JsonIgnore]
    public IReadOnlyDictionary<Guid, Guid> ProjectOrganizationIds { get; init; } = new Dictionary<Guid, Guid>();

    public static ManagementScope Empty { get; } = new(
        new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());
}

public sealed record ManagementContext(
    Guid UserId,
    Guid TenantId,
    ManagementPerspective SelectedPerspective,
    ManagementViewMode ViewMode,
    Guid? SelectedPortfolioId,
    Guid? SelectedProgramId,
    Guid? SelectedProjectId,
    ManagementScope Scope)
{
    public bool EffectiveReadOnly => ViewMode == ManagementViewMode.ReadOnlyViewAs;

    // Set by the API middleware only after checking the current tenant's
    // persisted system-admin role, and only for approved portfolio/program GETs.
    public bool CanReadAllTenantPortfolioProgramData { get; init; }

    // Grants tenant-wide object scope for the exact Portfolio PUT route only.
    // The endpoint's Portfolio.Edit policy remains independently required.
    public bool HasTenantAdministratorPortfolioWriteScope { get; init; }
}

public sealed record ManagementPerspectiveAvailability(
    ManagementPerspective Perspective,
    bool IsNative,
    bool RequiresEntityContext,
    IReadOnlyList<ManagementEntityReference> Contexts);

public sealed record ManagementPerspectiveSnapshot(
    IReadOnlyList<ManagementPerspective> NativePerspectives,
    IReadOnlyList<ManagementPerspectiveAvailability> AvailablePerspectives,
    ManagementPerspective? DefaultPerspective,
    bool CanViewAs,
    ManagementScope Scope,
    ManagementContext? SelectedContext);
