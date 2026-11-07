using System.ComponentModel.DataAnnotations;

namespace NEXUS.Modules.Organization.Application;

public sealed record OrganizationProfileDto(
    Guid Id,
    string Name,
    string? Description,
    string? Website,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? Postcode,
    DateTime? UpdatedAt,
    bool CanManageSettings = false);

public sealed class UpdateOrganizationProfileRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    [StringLength(500), Url]
    public string? Website { get; init; }

    [StringLength(254), EmailAddress]
    public string? Email { get; init; }

    [StringLength(100)]
    public string? Phone { get; init; }

    [StringLength(300)]
    public string? AddressLine1 { get; init; }

    [StringLength(300)]
    public string? AddressLine2 { get; init; }

    [StringLength(150)]
    public string? City { get; init; }

    [StringLength(150)]
    public string? Country { get; init; }

    [StringLength(30)]
    public string? Postcode { get; init; }

    public DateTime? ExpectedUpdatedAt { get; init; }
}

public enum OrganizationProfileUpdateCode
{
    Updated,
    NotFound,
    ConcurrencyConflict,
    TenantContextMissing,
    AuditFailed
}

public sealed record OrganizationProfileUpdateResult(
    OrganizationProfileUpdateCode Code,
    OrganizationProfileDto? Profile = null,
    string? Error = null);
