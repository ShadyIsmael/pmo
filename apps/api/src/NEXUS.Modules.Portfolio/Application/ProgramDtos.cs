namespace NEXUS.Modules.Portfolio.Application;

public sealed class ProgramDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public string? Status { get; set; }
    public Guid? ProgramManagerId { get; set; }
    public string? ProgramManagerName { get; set; }
    public string? ProgramManagerEmail { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? Budget { get; set; }
    public decimal? ActualSpend { get; set; }
    public string? Health { get; set; }
    public int ProjectCount { get; set; }
    public IReadOnlyList<ProgramProjectDto> Projects { get; set; } = Array.Empty<ProgramProjectDto>();
    public Dictionary<string, ProgramCurrencyFinancialSummary> ByCurrency { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class ProgramProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal? Budget { get; set; }
    public decimal? Forecast { get; set; }
    public decimal? ActualCost { get; set; }
}

public sealed class ProgramCurrencyFinancialSummary
{
    public string Currency { get; set; } = "";
    public bool IsBaseCurrency { get; set; }
    public int ProjectCount { get; set; }
    public decimal Budget { get; set; }
    public decimal Forecast { get; set; }
    public decimal ActualCost { get; set; }
}

public sealed class SaveProgramDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid? ProgramManagerId { get; set; }
    public string Status { get; set; } = "Planning";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Currency { get; set; }
    public decimal? Budget { get; set; }
    public string? Health { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
