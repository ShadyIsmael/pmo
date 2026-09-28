namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed class BudgetRevisionDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid BudgetId { get; init; }
    public string BudgetName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal PreviousApprovedAmount { get; init; }
    public decimal ProposedApprovedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Justification { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid RequestedBy { get; init; }
    public string? RequestedByName { get; init; }
    public DateTime RequestedAt { get; init; }
    public Guid? ReviewedBy { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewComment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class CreateBudgetRevisionDto
{
    public Guid BudgetId { get; init; }
    public decimal ProposedApprovedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Justification { get; init; }
}

public sealed class UpdateBudgetRevisionDto
{
    public decimal ProposedApprovedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Justification { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class SubmitBudgetRevisionDto
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class ReviewBudgetRevisionDto
{
    public string? ReviewComment { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}
