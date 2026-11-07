using System.Text.Json;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using NEXUS.Modules.Organization.Application.Settings;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class RiskActionFollowUpDateContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateAndUpdateDtos_AcceptDateOnlyAndRejectDateTimeValues()
    {
        var create = JsonSerializer.Deserialize<CreateRiskActionDto>(
            "{\"nextFollowUpDate\":\"2026-11-02\"}", WebJson);
        var update = JsonSerializer.Deserialize<UpdateRiskActionDto>(
            "{\"nextFollowUpDate\":\"2026-11-02\"}", WebJson);

        Assert.Equal(new DateOnly(2026, 11, 2), create!.NextFollowUpDate);
        Assert.Equal(new DateOnly(2026, 11, 2), update!.NextFollowUpDate);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateRiskActionDto>(
            "{\"nextFollowUpDate\":\"2026-11-02T15:30:00Z\"}", WebJson));
    }

    [Fact]
    public void ResponseDto_SerializesFollowUpAsAnIsoDateWithoutTime()
    {
        var json = JsonSerializer.Serialize(new RiskActionDto
        {
            NextFollowUpDate = new DateOnly(2026, 11, 2)
        }, WebJson);

        Assert.Contains("\"nextFollowUpDate\":\"2026-11-02\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkingCalendarSetting_AllowsNullableTenantAndOrganizationOverridesOnly()
    {
        var catalog = new SettingDefinitionCatalog();
        Assert.True(catalog.TryGet("workingCalendar.defaultCalendarId", out var definition));

        Assert.Contains(SettingScope.Tenant, definition.AllowedScopes);
        Assert.Contains(SettingScope.Organization, definition.AllowedScopes);
        Assert.DoesNotContain(SettingScope.Project, definition.AllowedScopes);
        Assert.Null(definition.Validate(JsonSerializer.SerializeToElement<string?>(null)));
        Assert.Null(definition.Validate(JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D"))));
        Assert.NotNull(definition.Validate(JsonSerializer.SerializeToElement(Guid.Empty.ToString("D"))));
        Assert.NotNull(definition.Validate(JsonSerializer.SerializeToElement("not-a-guid")));
    }
}
