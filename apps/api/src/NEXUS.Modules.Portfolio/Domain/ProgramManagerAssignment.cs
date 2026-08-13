using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class ProgramManagerAssignment : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid ProgramManagerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? HandoverFromProgramManagerId { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
}
