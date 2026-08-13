using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

public class DocumentReview : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid ReviewerId { get; set; }
    public string? Comments { get; set; }
    public string? Status { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? DueDate { get; set; }
}
