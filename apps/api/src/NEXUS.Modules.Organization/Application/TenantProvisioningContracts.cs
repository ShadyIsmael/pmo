using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Modules.Organization.Application;

public sealed record CreateTenantRequest(
    string Code,
    string DisplayName,
    string InitialOrganizationName,
    Guid? TenantId = null,
    Guid? InitialAdministratorUserId = null);

public sealed record TenantProvisioningResult(
    Guid TenantId,
    Guid OrganizationId,
    TenantLifecycleStatus Status,
    string? AdministratorRoleName = null);

public sealed record TenantSummary(Guid Id, string Code, string DisplayName, TenantLifecycleStatus Status, DateTime CreatedAt);

public enum TenantProvisioningCode
{
    Created,
    Activated,
    StatusChanged,
    ValidationFailed,
    DuplicateCode,
    TenantNotFound,
    AdministratorNotFound,
    AdministratorRoleUnavailable,
    ConcurrencyConflict,
    AuditUnavailable,
    InvalidLifecycleTransition
}

public sealed record TenantProvisioningOperationResult(
    TenantProvisioningCode Code,
    TenantProvisioningResult? Value = null,
    string? Error = null);
