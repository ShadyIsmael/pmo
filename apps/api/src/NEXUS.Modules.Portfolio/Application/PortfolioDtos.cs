namespace NEXUS.Modules.Portfolio.Application;

public sealed class PortfolioProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public string Health { get; set; } = "";
    public string RagStatus { get; set; } = "";
    public decimal Progress { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal? Forecast { get; set; }
    public decimal? ActualCost { get; set; }
    public string Currency { get; set; } = "GBP";
    public Guid? ProgramId { get; set; }
    public bool IsDirectProject { get; set; }
}

public sealed class PortfolioMetricsDto
{
    public int ProjectCount { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int DelayedProjects { get; set; }
    public decimal? TotalBudget { get; set; }
    public decimal? TotalForecast { get; set; }
    public decimal? TotalActualCost { get; set; }
    public decimal AverageProgress { get; set; }
    public DateTime? EarliestStartDate { get; set; }
    public DateTime? LatestEndDate { get; set; }
    public Dictionary<string, int> ProjectsByStatus { get; set; } = new();
    public Dictionary<string, int> ProjectsByHealth { get; set; } = new();
    public Dictionary<string, CurrencyFinancialSummary> ByCurrency { get; set; } = new();
}

public sealed class CurrencyFinancialSummary
{
    public string Currency { get; set; } = "";
    public bool IsBaseCurrency { get; set; }
    public int ProjectCount { get; set; }
    public decimal Budget { get; set; }
    public decimal Forecast { get; set; }
    public decimal ActualCost { get; set; }
}

public sealed class PortfolioDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public Guid OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StrategicObjective { get; set; }
    public string? Priority { get; set; }
    public string? Health { get; set; }
    public string? ConfidenceLevel { get; set; }
    public PortfolioMetricsDto Metrics { get; set; } = new();
    public IReadOnlyList<PortfolioProjectDto> Projects { get; set; } = Array.Empty<PortfolioProjectDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? UpdatedBy { get; set; }
}

public sealed class SavePortfolioDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string Status { get; set; } = "Active";
    public string Currency { get; set; } = "USD";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StrategicObjective { get; set; }
    public string? Priority { get; set; }
    public string? Health { get; set; }
    public string? ConfidenceLevel { get; set; }
}
