using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Application;

public static class DirectoryProviderHttpClients
{
    public const string Entra = "Directory.Entra";
    public const string Ldap = "Directory.Ldap";
}

public sealed record DirectoryProviderTestResult(
    bool Connected,
    string Provider,
    string? DirectoryName,
    long LatencyMilliseconds,
    string? ErrorCode,
    string? Message);

public sealed record DirectoryUserRecord(
    string ExternalDirectoryId,
    string? UserPrincipalName,
    string? Username,
    string? DisplayName,
    string? Email,
    string? Department,
    string? JobTitle,
    string? EmployeeId,
    string? ManagerExternalId,
    bool IsEnabled,
    IReadOnlyList<string> GroupIds,
    IReadOnlyDictionary<string, string?> Attributes,
    string? DistinguishedName = null,
    bool GroupMembershipsLoaded = false);

public sealed record DirectoryGroupRecord(
    string ExternalDirectoryId,
    string Name,
    string? Description,
    bool IsEnabled,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record DirectoryProviderPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);

public sealed record DirectoryProviderQuery(int Page, int PageSize, string? Search = null);

public interface IDirectorySecretResolver
{
    string? Resolve(Guid tenantId, Guid connectionId, string? reference);
}

public interface IDirectoryProvider
{
    string ProviderType { get; }

    Task<DirectoryProviderTestResult> TestConnectionAsync(
        DirectoryConnection connection,
        string? credential,
        CancellationToken cancellationToken = default);

    Task<DirectoryProviderPage<DirectoryUserRecord>> GetUsersAsync(
        DirectoryConnection connection,
        DirectorySyncScope scope,
        string? credential,
        DirectoryProviderQuery query,
        CancellationToken cancellationToken = default);

    Task<DirectoryProviderPage<DirectoryGroupRecord>> GetGroupsAsync(
        DirectoryConnection connection,
        string? credential,
        DirectoryProviderQuery query,
        CancellationToken cancellationToken = default);
}
