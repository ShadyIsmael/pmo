namespace NEXUS.Modules.Integrations.Application;

/// <summary>
/// Safe read projection for an integration connection. Credential fields
/// (<c>ApiKey</c>/<c>ApiSecret</c>) and free-form provider error logs are never
/// serialized to API clients; callers only learn whether a secret is configured.
/// </summary>
public sealed class IntegrationConnectionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? BaseUrl { get; init; }
    public string? AuthType { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastSyncAt { get; init; }
    public string? Status { get; init; }
    public bool HasApiKey { get; init; }
    public bool HasApiSecret { get; init; }
    public IReadOnlyList<IntegrationSyncDto> Syncs { get; init; } = Array.Empty<IntegrationSyncDto>();
}

public sealed class IntegrationSyncDto
{
    public Guid Id { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int? RecordsProcessed { get; init; }
    public int? RecordsFailed { get; init; }
}
