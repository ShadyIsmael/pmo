using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public interface IOrganizationalUnitService
{
    Task<IReadOnlyList<OrganizationalUnitDto>> GetUnitsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Result<OrganizationalUnitDto>> GetUnitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<OrganizationalUnitDto>> CreateUnitAsync(SaveOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result<OrganizationalUnitDto>> UpdateUnitAsync(Guid id, SaveOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> ReactivateUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationalUnitTypeDto>> GetTypesAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Result<OrganizationalUnitTypeDto>> CreateTypeAsync(SaveOrganizationalUnitTypeDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result<OrganizationalUnitTypeDto>> UpdateTypeAsync(Guid id, SaveOrganizationalUnitTypeDto request, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceOrganizationalUnitDto>> GetMembershipsAsync(Guid? organizationalUnitId, Guid? resourceId, bool includeHistory, CancellationToken cancellationToken = default);
    Task<Result<ResourceOrganizationalUnitDto>> AssignMembershipAsync(SaveResourceOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result> EndMembershipAsync(Guid membershipId, DateTime effectiveTo, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceReportingLineDto>> GetReportingLinesAsync(Guid? resourceId, bool includeHistory, CancellationToken cancellationToken = default);
    Task<Result<ResourceReportingLineDto>> AssignReportingLineAsync(SaveResourceReportingLineDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result> EndReportingLineAsync(Guid reportingLineId, DateTime effectiveTo, string userId, CancellationToken cancellationToken = default);
}
