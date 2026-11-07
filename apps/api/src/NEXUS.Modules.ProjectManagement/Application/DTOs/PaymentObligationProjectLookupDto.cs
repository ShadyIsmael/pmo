namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

/// <summary>Minimal project details available when selecting a project for a payment obligation.</summary>
public sealed record PaymentObligationProjectLookupDto(
    Guid Id,
    Guid PublicId,
    string Name,
    string? Code,
    string Currency);
