using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed class PlanningTablePreferenceService : IPlanningTablePreferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] RequiredColumns = ["schedulingMode", "id", "taskName"];
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanningTablePreferenceService(DbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<PlanningTablePreferenceDto>> GetAsync(Guid projectId, Guid planId, string userId, CancellationToken cancellationToken = default)
    {
        var validPlan = await PlanQuery(projectId, planId).AnyAsync(cancellationToken);
        if (!validPlan) return Result<PlanningTablePreferenceDto>.Failure(Error.NotFound("Plan not found"));
        var user = ParseUser(userId);
        if (!user.HasValue) return Result<PlanningTablePreferenceDto>.Failure(Error.Validation("Authenticated user is required"));
        var entity = await _context.Set<PlanningTablePreference>().FirstOrDefaultAsync(item => item.PlanId == planId && item.UserId == user.Value && !item.IsDeleted, cancellationToken);
        return Result<PlanningTablePreferenceDto>.Success(entity is null ? Defaults(planId) : ToDto(entity));
    }

    public async Task<Result<PlanningTablePreferenceDto>> SaveAsync(Guid projectId, Guid planId, SavePlanningTablePreferenceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var validPlan = await PlanQuery(projectId, planId).AnyAsync(cancellationToken);
        if (!validPlan) return Result<PlanningTablePreferenceDto>.Failure(Error.NotFound("Plan not found"));
        var user = ParseUser(userId);
        if (!user.HasValue) return Result<PlanningTablePreferenceDto>.Failure(Error.Validation("Authenticated user is required"));
        var normalized = Normalize(dto);
        var entity = await _context.Set<PlanningTablePreference>().FirstOrDefaultAsync(item => item.PlanId == planId && item.UserId == user.Value && !item.IsDeleted, cancellationToken);
        if (entity is not null && IsEquivalent(entity, normalized))
            return Result<PlanningTablePreferenceDto>.Success(ToDto(entity));

        if (entity is null)
        {
            entity = new PlanningTablePreference { TenantId = TenantId(), UserId = user.Value, PlanId = planId, CreatedBy = userId };
            _context.Set<PlanningTablePreference>().Add(entity);
        }
        entity.VisibleColumnsJson = JsonSerializer.Serialize(normalized.VisibleColumns, JsonOptions);
        entity.ColumnOrderJson = JsonSerializer.Serialize(normalized.ColumnOrder, JsonOptions);
        entity.ColumnWidthsJson = JsonSerializer.Serialize(normalized.ColumnWidths, JsonOptions);
        entity.ColumnFiltersJson = JsonSerializer.Serialize(normalized.ColumnFilters, JsonOptions);
        entity.CollapsedRowIdsJson = JsonSerializer.Serialize(normalized.CollapsedRowIds, JsonOptions);
        entity.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<PlanningTablePreferenceDto>.Success(ToDto(entity));
    }

    public async Task<Result> ResetAsync(Guid projectId, Guid planId, string userId, CancellationToken cancellationToken = default)
    {
        var validPlan = await PlanQuery(projectId, planId).AnyAsync(cancellationToken);
        if (!validPlan) return Result.Failure(Error.NotFound("Plan not found"));
        var user = ParseUser(userId);
        if (!user.HasValue) return Result.Failure(Error.Validation("Authenticated user is required"));
        var entity = await _context.Set<PlanningTablePreference>().FirstOrDefaultAsync(item => item.PlanId == planId && item.UserId == user.Value && !item.IsDeleted, cancellationToken);
        if (entity is not null)
        {
            entity.IsDeleted = true;
            entity.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Result.Success();
    }

    private IQueryable<Plan> PlanQuery(Guid projectId, Guid planId) => _context.Set<Plan>().AsNoTracking().Where(plan => plan.Id == planId && plan.ProjectId == projectId && !plan.IsDeleted);

    private static Guid? ParseUser(string value) => Guid.TryParse(value, out var parsed) ? parsed : null;
    private Guid TenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenant) ? tenant : Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static SavePlanningTablePreferenceDto Normalize(SavePlanningTablePreferenceDto dto)
    {
        var visible = RequiredColumns.Concat(dto.VisibleColumns).Distinct(StringComparer.Ordinal).ToList();
        var order = dto.ColumnOrder.Where(visible.Contains).Distinct(StringComparer.Ordinal).ToList();
        order.AddRange(visible.Where(item => !order.Contains(item, StringComparer.Ordinal)));
        var widths = dto.ColumnWidths.Where(item => item.Value is >= 24 and <= 1200).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var filters = dto.ColumnFilters.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var collapsed = dto.CollapsedRowIds.Distinct(StringComparer.Ordinal).ToList();
        return new SavePlanningTablePreferenceDto { VisibleColumns = visible, ColumnOrder = order, ColumnWidths = widths, ColumnFilters = filters, CollapsedRowIds = collapsed };
    }

    private static bool IsEquivalent(PlanningTablePreference entity, SavePlanningTablePreferenceDto normalized) =>
        entity.VisibleColumnsJson == JsonSerializer.Serialize(normalized.VisibleColumns, JsonOptions) &&
        entity.ColumnOrderJson == JsonSerializer.Serialize(normalized.ColumnOrder, JsonOptions) &&
        entity.ColumnWidthsJson == JsonSerializer.Serialize(normalized.ColumnWidths, JsonOptions) &&
        entity.ColumnFiltersJson == JsonSerializer.Serialize(normalized.ColumnFilters, JsonOptions) &&
        entity.CollapsedRowIdsJson == JsonSerializer.Serialize(normalized.CollapsedRowIds, JsonOptions);

    private static PlanningTablePreferenceDto Defaults(Guid planId) => new() { PlanId = planId };
    private static PlanningTablePreferenceDto ToDto(PlanningTablePreference entity)
    {
        return new PlanningTablePreferenceDto
        {
            PlanId = entity.PlanId,
            VisibleColumns = Deserialize<List<string>>(entity.VisibleColumnsJson) ?? [],
            ColumnOrder = Deserialize<List<string>>(entity.ColumnOrderJson) ?? [],
            ColumnWidths = Deserialize<Dictionary<string, int>>(entity.ColumnWidthsJson) ?? new(),
            ColumnFilters = Deserialize<Dictionary<string, PlanningColumnFilterDto>>(entity.ColumnFiltersJson) ?? new(),
            CollapsedRowIds = Deserialize<List<string>>(entity.CollapsedRowIdsJson) ?? []
        };
    }
    private static T? Deserialize<T>(string json) { try { return JsonSerializer.Deserialize<T>(json, JsonOptions); } catch (JsonException) { return default; } }
}
