using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    decimal ExchangeRate,
    bool IsBase);

public sealed record SaveCurrencyRequest(
    string Code,
    string Name,
    string Symbol,
    bool IsBase);

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<CurrencyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CurrencyDto>> CreateAsync(SaveCurrencyRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<CurrencyDto>> UpdateAsync(Guid id, SaveCurrencyRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}

public sealed class CurrencyService : ICurrencyService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrencyService(DbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var currencies = await _context.Set<Currency>()
            .Where(currency => !currency.IsDeleted)
            .OrderByDescending(currency => currency.IsBase)
            .ThenBy(currency => currency.Code)
            .ToListAsync(cancellationToken);

        return currencies.Select(ToDto).ToList();
    }

    public async Task<Result<CurrencyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var currency = await _context.Set<Currency>()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        return currency is null
            ? Result<CurrencyDto>.Failure(Error.NotFound("Currency not found"))
            : Result<CurrencyDto>.Success(ToDto(currency));
    }

    public async Task<Result<CurrencyDto>> CreateAsync(SaveCurrencyRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request, out var code, out var name, out var symbol);
        if (validation is not null) return Result<CurrencyDto>.Failure(validation);

        var tenantId = await GetTenantIdAsync(cancellationToken);
        if (!tenantId.HasValue) return Result<CurrencyDto>.Failure(Error.Validation("Tenant context is not available"));

        var duplicate = await _context.Set<Currency>()
            .IgnoreQueryFilters()
            .AnyAsync(currency => currency.TenantId == tenantId.Value && currency.Code == code, cancellationToken);
        if (duplicate) return Result<CurrencyDto>.Failure(Error.Conflict("A currency with this code already exists"));

        if (request.IsBase)
        {
            await ClearBaseCurrencyAsync(tenantId.Value, cancellationToken);
        }

        var currency = new Currency
        {
            TenantId = tenantId.Value,
            Code = code,
            Name = name,
            Symbol = symbol,
            IsBase = request.IsBase,
            ExchangeRate = 1m,
            CreatedBy = userId
        };

        _context.Set<Currency>().Add(currency);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<CurrencyDto>.Success(ToDto(currency));
    }

    public async Task<Result<CurrencyDto>> UpdateAsync(Guid id, SaveCurrencyRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request, out var code, out var name, out var symbol);
        if (validation is not null) return Result<CurrencyDto>.Failure(validation);

        var currency = await _context.Set<Currency>()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (currency is null) return Result<CurrencyDto>.Failure(Error.NotFound("Currency not found"));

        var duplicate = await _context.Set<Currency>()
            .IgnoreQueryFilters()
            .AnyAsync(item => item.Id != id && item.TenantId == currency.TenantId && item.Code == code, cancellationToken);
        if (duplicate) return Result<CurrencyDto>.Failure(Error.Conflict("A currency with this code already exists"));

        if (request.IsBase)
        {
            await ClearBaseCurrencyAsync(currency.TenantId, cancellationToken, id);
        }

        currency.Code = code;
        currency.Name = name;
        currency.Symbol = symbol;
        currency.IsBase = request.IsBase;
        currency.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CurrencyDto>.Success(ToDto(currency));
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var currency = await _context.Set<Currency>()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (currency is null) return Result.Failure(Error.NotFound("Currency not found"));
        if (currency.IsBase) return Result.Failure(Error.Validation("The base currency cannot be deleted"));

        currency.IsDeleted = true;
        currency.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Guid?> GetTenantIdAsync(CancellationToken cancellationToken)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId)) return tenantId;

        var seededTenantId = await _context.Set<OrganizationEntity>()
            .IgnoreQueryFilters()
            .Select(organization => (Guid?)organization.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        return seededTenantId == Guid.Empty ? null : seededTenantId;
    }

    private async Task ClearBaseCurrencyAsync(Guid tenantId, CancellationToken cancellationToken, Guid? exceptId = null)
    {
        var baseCurrencies = await _context.Set<Currency>()
            .Where(currency => currency.TenantId == tenantId && currency.IsBase && !currency.IsDeleted && (!exceptId.HasValue || currency.Id != exceptId.Value))
            .ToListAsync(cancellationToken);

        foreach (var currency in baseCurrencies) currency.IsBase = false;
    }

    private static Error? Validate(SaveCurrencyRequest request, out string code, out string name, out string symbol)
    {
        code = request.Code.Trim().ToUpperInvariant();
        name = request.Name.Trim();
        symbol = request.Symbol.Trim();

        if (code.Length != 3 || code.Any(character => character is < 'A' or > 'Z'))
            return Error.Validation("Currency code must be a three-letter ISO code");
        if (name.Length == 0) return Error.Validation("Currency name is required");
        if (symbol.Length == 0) return Error.Validation("Currency symbol is required");
        return null;
    }

    private static CurrencyDto ToDto(Currency currency) => new(
        currency.Id,
        currency.Code,
        currency.Name,
        currency.Symbol,
        currency.ExchangeRate,
        currency.IsBase);
}
