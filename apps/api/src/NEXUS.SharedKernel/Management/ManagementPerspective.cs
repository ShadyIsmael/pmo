namespace NEXUS.SharedKernel.Management;

/// <summary>Management presentation perspectives. This is not an authorization role.</summary>
public enum ManagementPerspective
{
    ProjectManager,
    ProgramManager,
    PortfolioManager
}

/// <summary>How the selected perspective is being used.</summary>
public enum ManagementViewMode
{
    Native,
    ReadOnlyViewAs
}

public static class ManagementPerspectiveRules
{
    public static int Level(ManagementPerspective perspective) => perspective switch
    {
        ManagementPerspective.ProjectManager => 1,
        ManagementPerspective.ProgramManager => 2,
        ManagementPerspective.PortfolioManager => 3,
        _ => 0
    };

    public static bool IsLower(ManagementPerspective native, ManagementPerspective selected)
        => Level(selected) < Level(native);
}
