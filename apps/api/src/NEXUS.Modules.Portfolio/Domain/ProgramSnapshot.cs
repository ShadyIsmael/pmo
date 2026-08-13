using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class ProgramSnapshot : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public int SnapshotNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveEndDate { get; set; }
    public Guid PreviousProgramManagerId { get; set; }
    public Guid NewProgramManagerId { get; set; }
    public string HandoverReason { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public string? Data { get; set; }
}
