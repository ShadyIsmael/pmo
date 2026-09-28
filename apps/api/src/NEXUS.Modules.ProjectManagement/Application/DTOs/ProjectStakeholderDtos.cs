using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class ProjectStakeholderDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "Stakeholder";
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public string Type { get; set; } = string.Empty;
    public ProjectStakeholderStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class AddInternalProjectStakeholderDto
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Interest { get; set; }
    public string? Influence { get; set; }
}

public sealed class AddContactProjectStakeholderDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Interest { get; set; }
    public string? Influence { get; set; }
}

public sealed class UpdateProjectStakeholderDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public byte[]? RowVersion { get; set; }
}

public sealed class RevokeProjectStakeholderDto
{
    public byte[]? RowVersion { get; set; }
}
