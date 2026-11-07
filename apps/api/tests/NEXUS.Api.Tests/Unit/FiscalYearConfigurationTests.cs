using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class FiscalYearConfigurationTests
{
    private readonly FiscalYearCalendarService _calendar = new();

    [Fact]
    public void NewTenantDefaultsToJanuaryAndJanuaryYearUsesCalendarLabel()
    {
        var catalog = new SettingDefinitionCatalog();
        Assert.True(catalog.TryGet(FiscalYearSetting.StartMonthKey, out var definition));
        Assert.Equal(1, definition.DefaultValue.GetInt32());
        Assert.Equal(new HashSet<SettingScope> { SettingScope.Tenant }, definition.AllowedScopes);
        Assert.Equal("FY 2027", _calendar.Resolve(new DateOnly(2027, 6, 30), 1).Label);
        Assert.Equal(new DateOnly(2027, 1, 1), _calendar.ForFiscalYear(2027, 1).StartDate);
        Assert.Equal(new DateOnly(2027, 12, 31), _calendar.ForFiscalYear(2027, 1).EndDate);
    }

    [Fact]
    public void AprilAndJulyFiscalYearsUseEndingYearLabels()
    {
        var april = _calendar.ForFiscalYear(2027, 4);
        var july = _calendar.ForFiscalYear(2027, 7);

        Assert.Equal("FY 2027", april.Label);
        Assert.Equal(new DateOnly(2026, 4, 1), april.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 31), april.EndDate);
        Assert.Equal("FY 2027", july.Label);
        Assert.Equal(new DateOnly(2026, 7, 1), july.StartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), july.EndDate);
    }

    [Fact]
    public void DecemberFiscalYearAndDateResolutionRespectBoundary()
    {
        var period = _calendar.ForFiscalYear(2027, 12);

        Assert.Equal(new DateOnly(2026, 12, 1), period.StartDate);
        Assert.Equal(new DateOnly(2027, 11, 30), period.EndDate);
        Assert.Equal(2027, _calendar.Resolve(new DateOnly(2026, 12, 1), 12).FiscalYear);
        Assert.Equal(2026, _calendar.Resolve(new DateOnly(2026, 11, 30), 12).FiscalYear);
        Assert.True(_calendar.IsInFiscalYear(new DateOnly(2027, 11, 30), 2027, 12));
        Assert.False(_calendar.IsInFiscalYear(new DateOnly(2027, 12, 1), 2027, 12));
    }

    [Fact]
    public void FiscalPeriodHandlesLeapDayAndUsesHalfOpenTimestampRange()
    {
        var period = _calendar.ForFiscalYear(2024, 3);
        var range = _calendar.ToTimestampRange(period, TimeZoneInfo.Utc);

        Assert.Equal(new DateOnly(2023, 3, 1), period.StartDate);
        Assert.Equal(new DateOnly(2024, 2, 29), period.EndDate);
        Assert.Equal(new DateOnly(2024, 3, 1), period.EndDateExclusive);
        Assert.True(period.Contains(new DateOnly(2024, 2, 29)));
        Assert.Equal(new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero), range.StartInclusive);
        Assert.Equal(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), range.EndExclusive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void InvalidStartMonthIsRejected(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calendar.Resolve(new DateOnly(2027, 1, 1), month));
        var catalog = new SettingDefinitionCatalog();
        Assert.True(catalog.TryGet(FiscalYearSetting.StartMonthKey, out var definition));
        Assert.NotNull(definition.Validate(JsonSerializer.SerializeToElement(month)));
    }

    [Fact]
    public async Task TenantFiscalYearServiceKeepsTenantCalendarsIsolated()
    {
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var repository = new FakeSettingsRepository();
        repository.SetTenant(firstTenant, FiscalYearSetting.StartMonthKey, 4);
        repository.SetTenant(secondTenant, FiscalYearSetting.StartMonthKey, 7);
        var service = CreateTenantFiscalService(repository);

        var first = await service.ResolveForDateAsync(firstTenant, new DateOnly(2026, 5, 1));
        var second = await service.ResolveForDateAsync(secondTenant, new DateOnly(2026, 5, 1));

        Assert.Equal(2027, first.FiscalYear);
        Assert.Equal(2026, second.FiscalYear);
        Assert.Contains(firstTenant, repository.TenantReads);
        Assert.Contains(secondTenant, repository.TenantReads);
    }

    [Fact]
    public async Task AuthenticatedTenantClaimScopesSettingsReadAndUnauthorizedWriteFailsClosed()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSettingsRepository();
        var service = CreateSettingsService(repository, CreateHttpContext(tenantId, Guid.NewGuid()));
        var read = await service.GetTenantAsync();
        var unauthorizedService = CreateSettingsService(repository, new DefaultHttpContext());
        var write = await unauthorizedService.UpdateTenantAsync(FiscalYearSetting.StartMonthKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement(4) });
        var update = typeof(SettingsController).GetMethod(nameof(SettingsController.UpdateTenant))!;
        var updatePolicy = update.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single().Policy;
        var preview = typeof(SettingsController).GetMethod(nameof(SettingsController.GetTenantFiscalYearPreview))!;
        var previewPolicy = preview.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single().Policy;

        Assert.True(read.Succeeded);
        Assert.Equal(tenantId, read.Value!.OwnerId);
        Assert.Contains(tenantId, repository.TenantReads);
        Assert.Equal(SettingsOperationCode.TenantContextMissing, write.Code);
        Assert.Equal("Settings.Write", updatePolicy);
        Assert.Equal("Settings.Read", previewPolicy);
    }

    [Fact]
    public async Task TenantSettingsServiceEnforcesReadAndWritePermissionsIndependently()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSettingsRepository();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "Settings.Read"))
            .ReturnsAsync(AuthorizationResult.Success());
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "Settings.Write"))
            .ReturnsAsync(AuthorizationResult.Failed());
        var service = CreateSettingsService(repository, CreateHttpContext(tenantId, Guid.NewGuid()),
            authorizationService: authorization.Object);

        var read = await service.GetTenantAsync();
        var write = await service.UpdateTenantAsync(FiscalYearSetting.StartMonthKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement(4) });

        Assert.True(read.Succeeded);
        Assert.Equal(SettingsOperationCode.AccessDenied, write.Code);
        Assert.Equal(0, repository.WriteCount);

        var deniedReadAuthorization = new Mock<IAuthorizationService>();
        deniedReadAuthorization.Setup(authorizer => authorizer.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "Settings.Read"))
            .ReturnsAsync(AuthorizationResult.Failed());
        var deniedRead = await CreateSettingsService(new FakeSettingsRepository(),
            CreateHttpContext(tenantId, Guid.NewGuid()), authorizationService: deniedReadAuthorization.Object)
            .GetTenantAsync();
        Assert.Equal(SettingsOperationCode.AccessDenied, deniedRead.Code);
    }

    [Fact]
    public async Task FiscalYearChangeAuditCapturesTenantActorUtcAndOldNewMonths()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeSettingsRepository();
        var auditEntry = (AuditEntry?)null;
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => auditEntry = entry)
            .ReturnsAsync(Result.Success());
        var service = CreateSettingsService(repository, CreateHttpContext(tenantId, actorId), audit.Object,
            new AllowFiscalYearChangeGuard());

        var result = await service.UpdateTenantAsync(FiscalYearSetting.StartMonthKey,
            new UpdateSettingRequest { Value = JsonSerializer.SerializeToElement(4) });

        Assert.True(result.Succeeded);
        Assert.NotNull(auditEntry);
        Assert.Equal(tenantId, auditEntry!.TenantId);
        Assert.Equal(actorId, auditEntry.UserId);
        Assert.Equal("FiscalYearStartMonthChanged", auditEntry.Action);
        Assert.Equal("TenantFiscalYearConfiguration", auditEntry.EntityType);
        Assert.Equal(DateTimeKind.Utc, auditEntry.Timestamp.Kind);
        using var original = JsonDocument.Parse(auditEntry.OriginalValues!);
        using var changed = JsonDocument.Parse(auditEntry.NewValues!);
        Assert.Equal(1, original.RootElement.GetProperty("fiscalYearStartMonth").GetInt32());
        Assert.Equal(4, changed.RootElement.GetProperty("fiscalYearStartMonth").GetInt32());
    }

    [Fact]
    public async Task AnyTenantBudgetHistoryBlocksCalendarChangesButNotOtherTenants()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(options);
        db.Budgets.Add(new Budget
        {
            TenantId = tenantId,
            Name = "Historical annual budget",
            Category = "Operating",
            FiscalYear = 2027,
            Status = "Approved"
        });
        await db.SaveChangesAsync();
        var guard = new FiscalYearConfigurationChangeGuard(db);

        var blocked = await guard.ValidateChangeAsync(tenantId, 1, 4);
        var otherTenant = await guard.ValidateChangeAsync(otherTenantId, 1, 4);
        var unchanged = await guard.ValidateChangeAsync(tenantId, 1, 1);

        Assert.Contains("fiscal-year budget records", blocked, StringComparison.OrdinalIgnoreCase);
        Assert.Null(otherTenant);
        Assert.Null(unchanged);
    }

    private static TenantFiscalYearService CreateTenantFiscalService(FakeSettingsRepository repository)
    {
        var catalog = new SettingDefinitionCatalog();
        return new TenantFiscalYearService(repository, new EffectiveConfigurationResolver(), catalog,
            new FiscalYearCalendarService());
    }

    private static ConfigurationSettingsService CreateSettingsService(
        FakeSettingsRepository repository,
        DefaultHttpContext httpContext,
        IAuditService? audit = null,
        IFiscalYearConfigurationChangeGuard? guard = null,
        IAuthorizationService? authorizationService = null)
    {
        var catalog = new SettingDefinitionCatalog();
        return new ConfigurationSettingsService(catalog, repository, new EffectiveConfigurationResolver(),
            new HttpContextAccessor { HttpContext = httpContext }, authorizationService ?? CreateAllowAllSettingsAuthorization(),
            audit ?? Mock.Of<IAuditService>(),
            Mock.Of<ICurrencyService>(), Mock.Of<IOrganizationSettingsAccessService>(), guard);
    }

    private static IAuthorizationService CreateAllowAllSettingsAuthorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        return authorization.Object;
    }

    private static DefaultHttpContext CreateHttpContext(Guid tenantId, Guid actorId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim(ClaimTypes.Name, "Tenant Administrator")
        }, "test"));
        return context;
    }

    private sealed class AllowFiscalYearChangeGuard : IFiscalYearConfigurationChangeGuard
    {
        public Task<string?> ValidateChangeAsync(Guid tenantId, int previousStartMonth, int newStartMonth,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<Guid, Dictionary<string, StoredConfigurationValue>> _tenantValues = new();
        public List<Guid> TenantReads { get; } = new();
        public int WriteCount { get; private set; }
        public bool IsAvailable => true;

        public void SetTenant(Guid tenantId, string key, int value)
        {
            if (!_tenantValues.TryGetValue(tenantId, out var settings))
                _tenantValues[tenantId] = settings = new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal);
            settings[key] = new StoredConfigurationValue(Guid.NewGuid(), key, JsonSerializer.SerializeToElement(value), "AQAAAAAAAAA=");
        }

        public Task<IReadOnlyDictionary<string, StoredConfigurationValue>> ReadAsync(
            SettingsScopeIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity.Scope == SettingScope.Tenant && identity.TenantId is { } tenantId)
            {
                TenantReads.Add(tenantId);
                if (_tenantValues.TryGetValue(tenantId, out var tenant))
                    return Task.FromResult<IReadOnlyDictionary<string, StoredConfigurationValue>>(tenant);
            }
            return Task.FromResult<IReadOnlyDictionary<string, StoredConfigurationValue>>(
                new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal));
        }

        public async Task<SettingsStoreWriteResult> UpsertAsync(SettingsScopeIdentity identity, string key,
            JsonElement value, string? expectedVersion, string actorId,
            Func<StoredConfigurationValue?, StoredConfigurationValue, CancellationToken, Task> appendAudit,
            CancellationToken cancellationToken = default,
            Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null)
        {
            WriteCount++;
            var tenantId = identity.TenantId!.Value;
            _tenantValues.TryGetValue(tenantId, out var settings);
            settings ??= _tenantValues[tenantId] = new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal);
            settings.TryGetValue(key, out var previous);
            if (validateConfigurationChange is not null)
            {
                var validationError = await validateConfigurationChange(previous?.Value, value, cancellationToken);
                if (validationError is not null)
                    return new SettingsStoreWriteResult(SettingsStoreWriteCode.ValidationFailed, Error: validationError);
            }
            var current = new StoredConfigurationValue(previous?.Id ?? Guid.NewGuid(), key, value.Clone(), "AQAAAAAAAAA=");
            settings[key] = current;
            await appendAudit(previous, current, cancellationToken);
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.Saved, current);
        }

        public Task<SettingsStoreWriteResult> ResetAsync(SettingsScopeIdentity identity, string key,
            string? expectedVersion, string actorId,
            Func<StoredConfigurationValue, CancellationToken, Task> appendAudit,
            CancellationToken cancellationToken = default,
            Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null) =>
            Task.FromResult(new SettingsStoreWriteResult(SettingsStoreWriteCode.NotFound));
    }
}
