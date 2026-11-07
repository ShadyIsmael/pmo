namespace NEXUS.Modules.Identity.Application;

/// <summary>Minimal identity fields returned by a workflow-scoped user picker.</summary>
public sealed record WorkflowUserLookupDto(Guid Id, string FullName);

/// <summary>Identity fields needed when linking a Resource to a system user.</summary>
public sealed record ResourceWorkflowUserLookupDto(Guid Id, string FullName, string Email);

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
}
