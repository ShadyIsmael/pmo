using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class SkillService : ISkillService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public SkillService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<PagedResult<SkillDto>> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Skill>().Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) ||
                (s.Category != null && s.Category.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SkillDto>(items.Select(s => s.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<Result<SkillDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await _context.Set<Skill>().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (skill is null)
            return Result<SkillDto>.Failure(Error.NotFound("Skill not found"));
        return Result<SkillDto>.Success(skill.ToDto());
    }

    public async Task<Result<SkillDto>> CreateAsync(CreateSkillDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Set<Skill>().AnyAsync(s => s.Name.ToLower() == dto.Name.ToLower() && !s.IsDeleted, cancellationToken);
        if (existing)
            return Result<SkillDto>.Failure(Error.Conflict("Skill with this name already exists"));

        var skill = dto.ToEntity();
        skill.CreatedBy = userId;

        _context.Set<Skill>().Add(skill);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Skill",
            EntityId = skill.Id,
            Action = "Create",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<SkillDto>.Success(skill.ToDto());
    }

    public async Task<Result<SkillDto>> UpdateAsync(Guid id, UpdateSkillDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var skill = await _context.Set<Skill>().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (skill is null)
            return Result<SkillDto>.Failure(Error.NotFound("Skill not found"));

        skill.ApplyUpdate(dto);
        skill.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Skill",
            EntityId = skill.Id,
            Action = "Update",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<SkillDto>.Success(skill.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var skill = await _context.Set<Skill>().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (skill is null)
            return Result.Failure(Error.NotFound("Skill not found"));

        skill.IsDeleted = true;
        skill.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Skill",
            EntityId = skill.Id,
            Action = "Delete",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }
}
