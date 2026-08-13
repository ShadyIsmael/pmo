using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
