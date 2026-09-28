using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Application.DTOs;

public sealed class CreateRiskDto
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public Guid? OwnerResourceId { get; set; }
    public CreateRiskImpactAssessmentDto? ImpactAssessment { get; set; }
}

public sealed class UpdateRiskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public Guid? OwnerResourceId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class CreateRiskImpactAssessmentDto
{
    public IReadOnlyList<RiskImpactType> ImpactTypes { get; set; } = Array.Empty<RiskImpactType>();
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateRiskImpactAssessmentDto
{
    public IReadOnlyList<RiskImpactType> ImpactTypes { get; set; } = Array.Empty<RiskImpactType>();
    public string Description { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RiskImpactAssessmentDto
{
    public Guid Id { get; set; }
    public Guid RiskId { get; set; }
    public IReadOnlyList<RiskImpactType> ImpactTypes { get; set; } = Array.Empty<RiskImpactType>();
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class UpdateRiskStatusDto
{
    public RiskStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RiskDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public RiskStatus Status { get; set; }
    public IReadOnlyList<RiskImpactType> ImpactTypes { get; set; } = Array.Empty<RiskImpactType>();
    public string? LastActionUpdateText { get; set; }
    public DateTime? LastActionUpdateAt { get; set; }
    public string? LastActionUpdateCreatedBy { get; set; }
    public Guid? OwnerResourceId { get; set; }
    public string? OwnerResourceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class CreateRiskActionDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerResourceId { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public RiskActionStatus? Status { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateRiskActionDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerResourceId { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public RiskActionStatus Status { get; set; }
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RiskActionDto
{
    public Guid Id { get; set; }
    public Guid RiskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerResourceId { get; set; }
    public string? OwnerResourceName { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public RiskActionStatus Status { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public int UpdateCount { get; set; }
}

public sealed class CreateRiskActionUpdateDto
{
    public string UpdateText { get; set; } = string.Empty;
}

public sealed class RiskActionUpdateDto
{
    public Guid Id { get; set; }
    public Guid RiskActionId { get; set; }
    public string UpdateText { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public RiskActionTimelineEntryType EntryType { get; set; }
    public IReadOnlyList<RiskActionChangeDto> Changes { get; set; } = Array.Empty<RiskActionChangeDto>();
}

public sealed class RiskActionChangeDto
{
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
