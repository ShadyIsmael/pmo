using NEXUS.SharedKernel;

namespace NEXUS.Modules.IssueManagement.Domain;

public class IssueAction : BaseEntity
{
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
