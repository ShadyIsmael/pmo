namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed record CreateProjectAnnualBudgetLineDto(string Description, decimal AuthorizedLimit);

public sealed record UpdateProjectAnnualBudgetLineDto(string Description, decimal AuthorizedLimit, byte[] RowVersion);

public sealed record DeleteProjectAnnualBudgetLineDto(byte[] RowVersion);

public sealed record ProjectAnnualBudgetLineDto(
    Guid Id,
    Guid AnnualBudgetId,
    string Reference,
    string Description,
    decimal AuthorizedLimit,
    decimal ReservedAmount,
    decimal AvailableAmount,
    string Currency,
    byte[] RowVersion,
    bool CanEdit);
