using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// Immutable, effective-dated reporting scope for a project. Every change
/// creates a new version so published reports can retain their exact scope.
/// </summary>
public sealed class ProjectReportingConfiguration : BaseEntity
{
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string ReportingFrequency { get; set; } = "Weekly";
    public string TimeZoneId { get; set; } = "UTC";
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Approved";
    public bool IsCurrent { get; set; }
    public string IncludedSectionsJson { get; set; } = "[]";
    public string ConfiguredBy { get; set; } = string.Empty;
    public DateTime ConfiguredAt { get; set; }

    public ICollection<ProjectReportingPlanSelection> Plans { get; set; } = new List<ProjectReportingPlanSelection>();
}

/// <summary>One plan's exact execution version and approved reference baseline.</summary>
public sealed class ProjectReportingPlanSelection : BaseEntity
{
    public Guid ProjectReportingConfigurationId { get; set; }
    public ProjectReportingConfiguration Configuration { get; set; } = null!;
    public Guid PlanId { get; set; }
    public Guid WorkingVersionId { get; set; }
    public Guid BaselineVersionId { get; set; }
    public bool IncludeInConsolidatedReporting { get; set; } = true;
    public string PlanNameSnapshot { get; set; } = string.Empty;
    public string PlanTypeSnapshot { get; set; } = string.Empty;
}
