namespace NEXUS.SharedKernel.Management;

public sealed record ManagementEntityReference(Guid Id, string Name, string? Code = null);

public sealed record ManagementScope(
    IReadOnlySet<Guid> PortfolioIds,
    IReadOnlySet<Guid> ProgramIds,
    IReadOnlySet<Guid> ProjectIds)
{
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
