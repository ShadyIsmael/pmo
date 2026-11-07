using System.Text.Json;
using NEXUS.Modules.Integrations.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for the integration response contract: credentials and provider error
/// logs must never be serialized to API clients.
/// </summary>
public class IntegrationConnectionDtoTests
{
    [Fact]
    public void Serialized_payload_exposes_only_non_secret_fields()
    {
        var dto = new IntegrationConnectionDto
        {
            Name = "GitLab",
            Provider = "gitlab",
            BaseUrl = "https://gitlab.example.com",
            AuthType = "token",
            IsActive = true,
            Status = "Connected",
            HasApiKey = true,
            HasApiSecret = true,
            Syncs = new[]
            {
                new IntegrationSyncDto
                {
                    Direction = "Inbound",
                    EntityType = "Project",
                    Status = "Succeeded",
                    RecordsProcessed = 12,
                    RecordsFailed = 0
                }
            }
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("\"ApiKey\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ApiSecret\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ErrorLog\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Config\":", json, StringComparison.Ordinal);
        Assert.Contains("\"HasApiKey\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"HasApiSecret\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Response_contract_does_not_declare_credential_properties()
    {
        var properties = typeof(IntegrationConnectionDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("ApiKey", properties);
        Assert.DoesNotContain("ApiSecret", properties);
        Assert.DoesNotContain("Config", properties);
    }
}
