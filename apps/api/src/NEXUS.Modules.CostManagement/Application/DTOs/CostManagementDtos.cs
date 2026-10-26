using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed class BudgetDto
{
    public Guid Id { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; init; }
    public Guid? PortfolioId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal ForecastAmount { get; init; }
    public string Currency { get; init; } = "GBP";
    public int FiscalYear { get; init; }
    public int? FiscalQuarter { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public class CreateBudgetDto
{
    public Guid? ProjectId { get; init; }
    public Guid? PortfolioId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal ForecastAmount { get; init; }
    public string Currency { get; init; } = "GBP";
    public int FiscalYear { get; init; }
    public int? FiscalQuarter { get; init; }
    public string Status { get; init; } = "Draft";
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed class UpdateBudgetDto : CreateBudgetDto
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class CostEntryDto
{
    public Guid Id { get; init; }
    public Guid? BudgetId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ResourceId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? SubCategory { get; init; }
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "GBP";
    public decimal? TaxAmount { get; init; }
    public DateTime IncurredDate { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public string? VendorName { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? IncurredBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public class CreateCostEntryDto
{
    public Guid? BudgetId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ResourceId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? SubCategory { get; init; }
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "GBP";
    public decimal? TaxAmount { get; init; }
    public DateTime IncurredDate { get; init; } = DateTime.UtcNow.Date;
    public string Type { get; init; } = "Actual";
    public string? Reference { get; init; }
    public string? VendorName { get; init; }
    public string Status { get; init; } = "Posted";
    public Guid? IncurredBy { get; init; }
}

public sealed class UpdateCostEntryDto : CreateCostEntryDto
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class DeleteFinancialRecordDto
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class PurchaseCommitmentDto
{
    public Guid Id { get; init; }
    public string PurchaseOrderNumber { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? VendorName { get; init; }
    public decimal Amount { get; init; }
    public decimal? InvoicedAmount { get; init; }
    public string Currency { get; init; } = "GBP";
    public DateTime CommitmentDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? BudgetId { get; init; }
    public Guid? ProjectId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class CreateCommitmentDto
{
    public Guid ProjectId { get; init; }
    public Guid? BudgetId { get; init; }
    public string PurchaseOrderNumber { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? VendorName { get; init; }
    public decimal Amount { get; init; }
    public DateTime CommitmentDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
}

public sealed class UpdateCommitmentDto
{
    public string PurchaseOrderNumber { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? VendorName { get; init; }
    public decimal Amount { get; init; }
    public DateTime CommitmentDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class PaymentSettlementDto
{
    public Guid Id { get; init; }
    public Guid PaymentObligationId { get; init; }
    public Guid IdempotencyKey { get; init; }
    public decimal Amount { get; init; }
    public DateTime SettlementDate { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class RecordPaymentSettlementDto
{
    public Guid IdempotencyKey { get; init; }
    public decimal Amount { get; init; }
    public DateTime SettlementDate { get; init; } = DateTime.UtcNow;
    public string PaymentReference { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class PaymentObligationDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid? MilestoneId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "GBP";
    public DateTime DueDate { get; init; }
    public string? Notes { get; init; }
    public decimal SettledAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public string LifecycleStatus { get; init; } = string.Empty;
    public bool IsDue { get; init; }
    public bool IsOverdue { get; init; }
    public IReadOnlyList<PaymentSettlementDto> Settlements { get; init; } = Array.Empty<PaymentSettlementDto>();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public class CreatePaymentObligationDto
{
    public Guid ProjectId { get; init; }
    public Guid? MilestoneId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public DateTime DueDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdatePaymentObligationDto : CreatePaymentObligationDto
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public static class CostManagementMappings
{
    public static BudgetDto ToDto(this Budget value, Guid? projectPublicId = null) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ProjectPublicId = projectPublicId, PortfolioId = value.PortfolioId,
        Name = value.Name, Description = value.Description, Category = value.Category,
        PlannedAmount = value.PlannedAmount, ApprovedAmount = value.ApprovedAmount,
        ForecastAmount = value.ForecastAmount, Currency = value.Currency,
        FiscalYear = value.FiscalYear, FiscalQuarter = value.FiscalQuarter, Status = value.Status,
        StartDate = value.StartDate, EndDate = value.EndDate, CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt, RowVersion = value.RowVersion
    };

    public static CostEntryDto ToDto(this CostEntry value) => new()
    {
        Id = value.Id, BudgetId = value.BudgetId, ProjectId = value.ProjectId, ResourceId = value.ResourceId,
        Category = value.Category, SubCategory = value.SubCategory, Description = value.Description,
        Amount = value.Amount, Currency = value.Currency, TaxAmount = value.TaxAmount,
        IncurredDate = value.IncurredDate, Type = value.Type, Reference = value.Reference,
        VendorName = value.VendorName, Status = value.Status, IncurredBy = value.IncurredBy,
        CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt, RowVersion = value.RowVersion
    };

    public static PurchaseCommitmentDto ToDto(this PurchaseCommitment value) => new()
    {
        Id = value.Id, PurchaseOrderNumber = value.PurchaseOrderNumber, Description = value.Description,
        VendorName = value.VendorName, Amount = value.Amount, InvoicedAmount = value.InvoicedAmount,
        Currency = value.Currency, CommitmentDate = value.CommitmentDate,
        ExpectedDeliveryDate = value.ExpectedDeliveryDate, Status = value.Status,
        BudgetId = value.BudgetId, ProjectId = value.ProjectId, CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt, RowVersion = value.RowVersion
    };

    public static PaymentSettlementDto ToDto(this PaymentSettlement value) => new()
    {
        Id = value.Id,
        PaymentObligationId = value.PaymentObligationId,
        IdempotencyKey = value.IdempotencyKey,
        Amount = value.Amount,
        SettlementDate = value.SettlementDate,
        PaymentReference = value.PaymentReference,
        Notes = value.Notes,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt,
        RowVersion = value.RowVersion
    };

    public static PaymentObligationDto ToDto(this PaymentObligation value) => new()
    {
        Id = value.Id,
        ProjectId = value.ProjectId,
        ProjectName = value.ProjectName,
        MilestoneId = value.MilestoneId,
        PaymentReference = value.PaymentReference,
        Purpose = value.Purpose,
        Description = value.Description,
        Amount = value.Amount,
        Currency = value.Currency,
        DueDate = value.DueDate,
        Notes = value.Notes,
        SettledAmount = value.SettledAmount,
        OutstandingAmount = value.OutstandingAmount,
        LifecycleStatus = value.GetLifecycleStatus(DateTime.UtcNow),
        IsDue = value.IsDue(DateTime.UtcNow),
        IsOverdue = value.IsOverdue(DateTime.UtcNow),
        Settlements = value.Settlements.Where(item => !item.IsDeleted).OrderByDescending(item => item.SettlementDate).Select(item => item.ToDto()).ToList(),
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt,
        RowVersion = value.RowVersion
    };
}
