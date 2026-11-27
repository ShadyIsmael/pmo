using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public class UserOrganization : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
}