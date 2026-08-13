using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Domain;

public class AIPlanResponse : BaseEntity
{
    public Guid AIPlanRequestId { get; set; }
    public AIPlanRequest AIPlanRequest { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int? ConfidenceScore { get; set; }
    public string? Suggestions { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
