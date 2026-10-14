using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public enum PermissionOverrideEffect
{
    Inherit = 0,
    Allow = 1,
    Deny = 2
}

public sealed class UserPermissionOverride : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
    public PermissionOverrideEffect Effect { get; set; } = PermissionOverrideEffect.Inherit;
}
