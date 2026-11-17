using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ResourceManagement.Application;

public class VendorService : IVendorService
{
    private const string CodePrefix = "VND-";
    private const int CodeDigits = 6;
    private const int CodeGenerationAttempts = 3;

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public VendorService(DbContext context, IAuditService auditService, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<VendorDto>> GetPagedAsync(int page, int pageSize, string? search = null, Guid? categoryId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Set<Vendor>().Where(v => !v.IsDeleted);

        if (isActive.HasValue)
            query = query.Where(v => v.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(term) ||
                (v.Code != null && v.Code.ToLower().Contains(term)) ||
                (v.ContactName != null && v.ContactName.ToLower().Contains(term)) ||
                (v.Category != null && v.Category.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(v => v.VendorCategoryId == categoryId.Value);

        var total = await query.CountAsync(cancellationToken);
        var entities = await query.OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await MapAllAsync(entities, cancellationToken);
        return new PagedResult<VendorDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ResourceVendorLookupDto>> GetResourceVendorOptionsAsync(Guid? resourceId = null, CancellationToken cancellationToken = default)
    {
        Guid? currentVendorId = null;
        if (resourceId.HasValue)
        {
            currentVendorId = await _context.Set<Resource>()
                .AsNoTracking()
                .Where(resource => resource.Id == resourceId.Value && !resource.IsDeleted)
                .Select(resource => resource.VendorId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _context.Set<Vendor>()
            .AsNoTracking()
            .Where(vendor => !vendor.IsDeleted
                && (vendor.IsActive || (currentVendorId.HasValue && vendor.Id == currentVendorId.Value)))
            .OrderBy(vendor => vendor.Name)
            .Select(vendor => new ResourceVendorLookupDto(vendor.Id, vendor.Name, vendor.Code, vendor.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<VendorDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));
        var dto = vendor.ToDto();
        await EnrichAsync(dto, cancellationToken);
        return Result<VendorDto>.Success(dto);
    }

    public async Task<Result<VendorDto>> CreateAsync(CreateVendorDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsTrustedActor(userId))
            return Result<VendorDto>.Failure(Error.Unauthorized("A valid authenticated actor is required."));

        var validation = Validate(dto);
        if (validation is not null)
            return Result<VendorDto>.Failure(validation);

        var category = await FindCategoryAsync(dto.VendorCategoryId!.Value, cancellationToken);
        if (category is null)
            return Result<VendorDto>.Failure(Error.Validation("Vendor category was not found"));
        if (!category.IsActive)
            return Result<VendorDto>.Failure(Error.Validation("Vendor category is not active"));

        var vendor = dto.ToEntity();
        vendor.TenantId = CurrentTenantId();
        vendor.CreatedBy = userId;

        var codeResult = await AssignCodeAsync(vendor, cancellationToken);
        if (codeResult.IsFailure)
            return Result<VendorDto>.Failure(codeResult.Error!);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Create",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = vendor.ToDto();
        await EnrichAsync(resultDto, cancellationToken);
        return Result<VendorDto>.Success(resultDto);
    }

    public async Task<Result<VendorDto>> UpdateAsync(Guid id, UpdateVendorDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsTrustedActor(userId))
            return Result<VendorDto>.Failure(Error.Unauthorized("A valid authenticated actor is required."));

        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));

        var validation = ValidateUpdate(dto);
        if (validation is not null)
            return Result<VendorDto>.Failure(validation);

        if (dto.VendorCategoryId.HasValue && dto.VendorCategoryId.Value != Guid.Empty && dto.VendorCategoryId.Value != vendor.VendorCategoryId)
        {
            var category = await FindCategoryAsync(dto.VendorCategoryId.Value, cancellationToken);
            if (category is null)
                return Result<VendorDto>.Failure(Error.Validation("Vendor category was not found"));
            if (!category.IsActive)
                return Result<VendorDto>.Failure(Error.Validation("An inactive vendor category cannot be assigned"));
        }

        vendor.ApplyUpdate(dto);
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Update",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = vendor.ToDto();
        await EnrichAsync(resultDto, cancellationToken);
        return Result<VendorDto>.Success(resultDto);
    }

    public async Task<Result<VendorDto>> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsTrustedActor(userId))
            return Result<VendorDto>.Failure(Error.Unauthorized("A valid authenticated actor is required."));

        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));

        vendor.IsActive = false;
        vendor.Status = "Inactive";
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Deactivate",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var dto = vendor.ToDto();
        await EnrichAsync(dto, cancellationToken);
        return Result<VendorDto>.Success(dto);
    }

    public async Task<Result<VendorDto>> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsTrustedActor(userId))
            return Result<VendorDto>.Failure(Error.Unauthorized("A valid authenticated actor is required."));

        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));

        vendor.IsActive = true;
        vendor.Status = "Active";
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Reactivate",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var dto = vendor.ToDto();
        await EnrichAsync(dto, cancellationToken);
        return Result<VendorDto>.Success(dto);
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        if (!IsTrustedActor(userId))
            return Result.Failure(Error.Unauthorized("A valid authenticated actor is required."));

        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result.Failure(Error.NotFound("Vendor not found"));

        var referencingResources = await _context.Set<Resource>()
            // Soft-deleted Resources still represent preserved business
            // history. Do not remove the Vendor reference while any such row
            // exists, even if it is not visible in the active management list.
            .Where(r => r.VendorId == id)
            .CountAsync(cancellationToken);
        if (referencingResources > 0)
            return Result.Failure(Error.Conflict(
                $"This vendor cannot be deleted because it is referenced by {referencingResources} outsourced resource{(referencingResources == 1 ? "" : "s")}. Deactivate it instead."));

        vendor.IsDeleted = true;
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Delete",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    private static Error? Validate(CreateVendorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Error.Validation("Vendor name is required");
        if (dto.Name.Trim().Length > 200)
            return Error.Validation("Vendor name must be 200 characters or fewer");
        if (!string.IsNullOrWhiteSpace(dto.ContactEmail) && !IsValidEmail(dto.ContactEmail.Trim()))
            return Error.Validation("Contact email is not valid");
        if (dto.ContractValue is < 0)
            return Error.Validation("Contract value cannot be negative");
        if (!dto.VendorCategoryId.HasValue || dto.VendorCategoryId.Value == Guid.Empty)
            return Error.Validation("Vendor category is required");
        return null;
    }

    private static Error? ValidateUpdate(UpdateVendorDto dto)
    {
        if (dto.Name != null && string.IsNullOrWhiteSpace(dto.Name))
            return Error.Validation("Vendor name is required");
        if (dto.Name != null && dto.Name.Trim().Length > 200)
            return Error.Validation("Vendor name must be 200 characters or fewer");
        if (!string.IsNullOrWhiteSpace(dto.ContactEmail) && !IsValidEmail(dto.ContactEmail.Trim()))
            return Error.Validation("Contact email is not valid");
        if (dto.ContractValue is < 0)
            return Error.Validation("Contract value cannot be negative");
        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private Guid CurrentTenantId() =>
        Guid.TryParse(_httpContextAccessor?.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : throw new InvalidOperationException("A trusted tenant context is required.");

    private bool IsTrustedActor(string userId)
    {
        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty)
            return false;

        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext is null)
            return false;

        var principal = httpContext.User;
        var actorClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = actorClaim?.Value ?? (actorClaim is null ? principal.FindFirst("sub")?.Value : null);
        return principal.Identity?.IsAuthenticated == true
            && Guid.TryParse(actorValue, out var claimActorId)
            && claimActorId == actorId;
    }

    private async Task<Lookup?> FindCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await _context.Set<Lookup>().FirstOrDefaultAsync(l =>
            l.Id == categoryId &&
            l.Category == ReferenceDataCategories.VendorCategory &&
            !l.IsDeleted,
            cancellationToken);
    }

    /// <summary>
    /// Generates the next tenant-scoped VND-000001 style code and saves the vendor.
    /// A serializable transaction (when the provider supports it) serializes the
    /// max-code read against concurrent creates, and the unique (TenantId, Code)
    /// index backstops any residual race; unique-violation failures retry with a
    /// freshly generated code.
    /// </summary>
    private async Task<Result> AssignCodeAsync(Vendor vendor, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= CodeGenerationAttempts; attempt++)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                if (_context.Database.IsRelational())
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                vendor.Code = await NextCodeAsync(cancellationToken);
                _context.Set<Vendor>().Add(vendor);
                await _context.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateException) when (attempt < CodeGenerationAttempts)
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                _context.Entry(vendor).State = EntityState.Detached;
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                _context.Entry(vendor).State = EntityState.Detached;
                return Result.Failure(Error.Conflict("Vendor code generation conflicted with a concurrent request. Please retry."));
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }

        return Result.Failure(Error.Conflict("Vendor code generation conflicted with a concurrent request. Please retry."));
    }

    private async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        var codes = await _context.Set<Vendor>()
            .Where(v => v.Code != null && v.Code.StartsWith(CodePrefix))
            .Select(v => v.Code!)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code[CodePrefix.Length..];
            if (int.TryParse(suffix, out var value) && value > max)
                max = value;
        }

        return $"{CodePrefix}{(max + 1).ToString($"D{CodeDigits}")}";
    }

    private async Task<List<VendorDto>> MapAllAsync(List<Vendor> entities, CancellationToken cancellationToken)
    {
        var dtos = entities.Select(v => v.ToDto()).ToList();
        foreach (var dto in dtos)
            await EnrichAsync(dto, cancellationToken);
        return dtos;
    }

    private async Task EnrichAsync(VendorDto dto, CancellationToken cancellationToken)
    {
        if (!dto.VendorCategoryId.HasValue || dto.CategoryName != null)
            return;

        var category = await _context.Set<Lookup>().AsNoTracking().FirstOrDefaultAsync(l =>
            l.Id == dto.VendorCategoryId.Value &&
            l.Category == ReferenceDataCategories.VendorCategory &&
            !l.IsDeleted,
            cancellationToken);
        dto.CategoryName = category?.Name;
    }
}
