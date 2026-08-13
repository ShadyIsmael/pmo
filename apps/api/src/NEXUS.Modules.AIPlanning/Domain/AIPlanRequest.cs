using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Domain;

public class AIPlanRequest : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Context { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Parameters { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<AIPlanResponse> Responses { get; set; } = new List<AIPlanResponse>();
}
