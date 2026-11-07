using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public class Risk : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? CategoryId { get; set; }
    public RiskCategory? Category { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public RiskStatus Status { get; set; }
    // Retained for compatibility with existing project snapshot projections.
    public decimal? PotentialCost { get; set; }
    public decimal? PotentialDelay { get; set; }
    public string? RiskResponse { get; set; }
    // Legacy compatibility field. Current API writes use OwnerResourceId or OwnerUserId.
    public Guid? OwnerId { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? OwnerResourceId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public ICollection<RiskAction> Actions { get; set; } = new List<RiskAction>();
    public ICollection<RiskImpactAssessment> ImpactAssessments { get; set; } = new List<RiskImpactAssessment>();
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RiskStatus
{
    Identified,
    ActionRequired,
    FollowUp,
    Mitigated,
    Closed,

    // Compatibility aliases for clients compiled against the original Risk API.
    [Obsolete("Use Identified")]
    Expected = Identified,
    [Obsolete("Use FollowUp")]
    Active = FollowUp
}

public enum RiskImpactType
{
    Schedule,
    Budget,
    Scope,
    Quality,
    Resources,
    Procurement,
    Compliance,
    Reputation,
    Operations,
    Other
}

public sealed class RiskImpactAssessment : BaseEntity
{
    public Guid RiskId { get; set; }
    public Risk Risk { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public ICollection<RiskImpactAssessmentType> ImpactTypes { get; set; } = new List<RiskImpactAssessmentType>();
}

public sealed class RiskImpactAssessmentType : BaseEntity
{
    public Guid RiskImpactAssessmentId { get; set; }
    public RiskImpactAssessment RiskImpactAssessment { get; set; } = null!;
    public RiskImpactType ImpactType { get; set; }
}
