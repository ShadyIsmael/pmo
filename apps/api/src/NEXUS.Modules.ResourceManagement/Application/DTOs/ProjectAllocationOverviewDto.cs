namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public sealed class ProjectAllocationOverviewDto
{
    public int ActiveAllocationCount { get; init; }
    public int ActiveResourceCount { get; init; }
    public decimal AllocatedHoursPerWeek { get; init; }
}
