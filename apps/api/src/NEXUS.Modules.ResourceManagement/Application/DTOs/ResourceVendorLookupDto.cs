namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

/// <summary>Minimal Vendor selection fields used by Resource forms.</summary>
public sealed record ResourceVendorLookupDto(Guid Id, string Name, string? Code, bool IsActive);

