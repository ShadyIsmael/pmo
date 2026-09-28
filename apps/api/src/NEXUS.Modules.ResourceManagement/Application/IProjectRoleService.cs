using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface IProjectRoleService
{
    Task<Result<IReadOnlyList<LookupDto>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LookupDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> CreateAsync(CreateLookupDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<LookupDto>> UpdateAsync(Guid id, UpdateLookupDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}

public class LookupDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
}

public class CreateLookupDto
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
}

public class UpdateLookupDto
{
    public string? Code { get; set; }
    public string? NameEn { get; set; }
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
    public string? Color { get; set; }
}
