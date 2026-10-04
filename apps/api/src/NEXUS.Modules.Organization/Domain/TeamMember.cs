using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>
/// Effective-dated membership of a Resource in a Team.
///
/// A Resource may belong to no Team, one Team, or several Teams; the current UI
/// offers one Team per Resource but the model supports many.
///
/// <see cref="ResourceId"/> is stored as a plain identifier on purpose so the
/// Organization module does not take a compile-time dependency on ResourceManagement.
/// Resource-facing concerns (member names, capacity) are resolved by the Team service
/// in ResourceManagement.
///
/// Membership is never hard-deleted. When a Resource leaves a Team, the row is closed
/// with <see cref="EffectiveTo"/> so historical assignments and time entries remain
/// attributable.
/// </summary>
public class TeamMember : BaseEntity
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid ResourceId { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>Null while the membership is current.</summary>
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>True when the membership is current: active and within its effective window.</summary>
    public bool IsCurrent => IsActive && !IsDeleted && (EffectiveTo is null || EffectiveTo > DateTime.UtcNow);
}
