using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Identity.Client;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Infrastructure;

public sealed class EntraDirectoryProvider(IHttpClientFactory httpClientFactory) : IDirectoryProvider
{
    private const string GraphRoot = "https://graph.microsoft.com/v1.0";
    private const string UserSelect = "id,userPrincipalName,displayName,givenName,surname,mail,department,jobTitle,employeeId,accountEnabled";
    private const string UserExpand = "manager($select=id)";
    private const int MaximumProviderRows = 5001;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderType => DirectoryProviderTypes.Entra;

    public async Task<DirectoryProviderTestResult> TestConnectionAsync(DirectoryConnection connection, string? credential, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var token = await AcquireTokenAsync(connection, credential, cancellationToken);
            await GetJsonAsync(token, $"{GraphRoot}/users?$select=id&$top=1", cancellationToken);
            await GetJsonAsync(token, $"{GraphRoot}/groups?$select=id&$top=1", cancellationToken);
            timer.Stop();
            return new DirectoryProviderTestResult(true, ProviderType, connection.DirectoryTenantId, timer.ElapsedMilliseconds, null, "Connected. User.Read.All and GroupMember.Read.All access were verified.");
        }
        catch (DirectoryProviderException exception)
        {
            timer.Stop();
            return new DirectoryProviderTestResult(false, ProviderType, connection.DirectoryTenantId, timer.ElapsedMilliseconds, exception.Code, exception.Message);
        }
        catch (MsalException)
        {
            timer.Stop();
            return new DirectoryProviderTestResult(false, ProviderType, null, timer.ElapsedMilliseconds, "ENTRA_AUTH_FAILED", "Entra authentication failed. Verify the tenant, client ID, credential reference, and app registration.");
        }
        catch (HttpRequestException)
        {
            timer.Stop();
            return new DirectoryProviderTestResult(false, ProviderType, null, timer.ElapsedMilliseconds, "ENTRA_UNREACHABLE", "Microsoft Entra or Microsoft Graph could not be reached from the API host.");
        }
    }

    public async Task<DirectoryProviderPage<DirectoryUserRecord>> GetUsersAsync(DirectoryConnection connection, DirectorySyncScope scope, string? credential, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        var token = await AcquireTokenAsync(connection, credential, cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumProviderRows - 1);
        var maxItems = Math.Clamp(scope.MaxItems + 1, 2, MaximumProviderRows);
        var wanted = Math.Min(maxItems, page * pageSize + 1);
        var users = scope.ScopeType switch
        {
            DirectoryScopeTypes.SelectedUsers => await GetSelectedUsersAsync(token, scope, wanted, cancellationToken),
            DirectoryScopeTypes.SelectedGroups => await GetGroupMembersAsync(token, scope, wanted, cancellationToken),
            DirectoryScopeTypes.OrganizationalUnits => await GetUsersByDepartmentsAsync(token, scope, wanted, cancellationToken),
            DirectoryScopeTypes.AllUsers => await GetCollectionAsync(token, $"{GraphRoot}/users?$select={UserSelect}&$expand={Uri.EscapeDataString(UserExpand)}&$top=100", wanted, cancellationToken),
            _ => []
        };

        if (!string.IsNullOrWhiteSpace(scope.UserFilter))
            users = users.Where(user => MatchesConfiguredFilter(user, scope.UserFilter)).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(user => Contains(user.DisplayName, search) || Contains(user.Email, search) || Contains(user.UserPrincipalName, search)).ToList();
        }

        var offset = (page - 1) * pageSize;
        return new DirectoryProviderPage<DirectoryUserRecord>(users.Skip(offset).Take(pageSize).ToArray(), page, pageSize, users.Count > offset + pageSize);
    }

    public async Task<DirectoryProviderPage<DirectoryGroupRecord>> GetGroupsAsync(DirectoryConnection connection, string? credential, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        var token = await AcquireTokenAsync(connection, credential, cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumProviderRows - 1);
        var groups = await GetGroupCollectionAsync(token, $"{GraphRoot}/groups?$select=id,displayName,description&$top=100", Math.Min(MaximumProviderRows, page * pageSize + 1), cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            groups = groups.Where(group => Contains(group.Name, search) || Contains(group.Description, search)).ToList();
        }
        var offset = (page - 1) * pageSize;
        return new DirectoryProviderPage<DirectoryGroupRecord>(groups.Skip(offset).Take(pageSize).ToArray(), page, pageSize, groups.Count > offset + pageSize);
    }

    private async Task<List<DirectoryUserRecord>> GetSelectedUsersAsync(string token, DirectorySyncScope scope, int wanted, CancellationToken cancellationToken)
    {
        var ids = ReadIds(scope.SelectedUserIdsJson).Take(Math.Min(wanted, MaximumProviderRows)).ToArray();
        var result = new List<DirectoryUserRecord>(ids.Length);
        foreach (var id in ids)
        {
            var escaped = Uri.EscapeDataString(id);
            try
            {
                using var document = await GetJsonAsync(token, $"{GraphRoot}/users/{escaped}?$select={UserSelect}&$expand={Uri.EscapeDataString(UserExpand)}", cancellationToken);
                if (TryMapUser(document.RootElement, out var user)) result.Add(user);
            }
            catch (DirectoryProviderException exception) when (exception.HttpStatus == HttpStatusCode.NotFound)
            {
                // A selected ID that no longer exists is represented through its persisted link by preview.
            }
        }
        return result;
    }

    private async Task<List<DirectoryUserRecord>> GetGroupMembersAsync(string token, DirectorySyncScope scope, int wanted, CancellationToken cancellationToken)
    {
        var groupIds = ReadIds(scope.SelectedGroupIdsJson).Take(50).ToArray();
        var result = new Dictionary<string, DirectoryUserRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupId in groupIds)
        {
            var url = $"{GraphRoot}/groups/{Uri.EscapeDataString(groupId)}/members/microsoft.graph.user?$select={UserSelect}&$expand={Uri.EscapeDataString(UserExpand)}&$top=100";
            while (!string.IsNullOrWhiteSpace(url) && result.Count < wanted)
            {
                using var document = await GetJsonAsync(token, url, cancellationToken);
                foreach (var element in ReadValues(document.RootElement))
                {
                    if (TryMapUser(element, out var user))
                    {
                        if (result.TryGetValue(user.ExternalDirectoryId, out var existing))
                            result[user.ExternalDirectoryId] = existing with { GroupIds = existing.GroupIds.Append(groupId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), GroupMembershipsLoaded = true };
                        else
                            result.Add(user.ExternalDirectoryId, user with { GroupIds = [groupId], GroupMembershipsLoaded = true });
                    }
                    if (result.Count >= wanted) break;
                }
                url = ReadNextLink(document.RootElement);
            }
            if (result.Count >= wanted) break;
        }
        return result.Values.ToList();
    }

    private async Task<List<DirectoryUserRecord>> GetUsersByDepartmentsAsync(string token, DirectorySyncScope scope, int wanted, CancellationToken cancellationToken)
    {
        var departments = ReadIds(scope.OrganizationalUnitsJson).Take(50).ToArray();
        if (departments.Length == 0) return [];
        var filter = string.Join(" or ", departments.Select(value => $"department eq '{value.Replace("'", "''", StringComparison.Ordinal)}'"));
        var url = $"{GraphRoot}/users?$select={UserSelect}&$expand={Uri.EscapeDataString(UserExpand)}&$top=100&$filter={Uri.EscapeDataString(filter)}";
        return await GetCollectionAsync(token, url, wanted, cancellationToken);
    }

    private async Task<List<DirectoryUserRecord>> GetCollectionAsync(string token, string? url, int wanted, CancellationToken cancellationToken)
    {
        var result = new List<DirectoryUserRecord>(Math.Min(wanted, MaximumProviderRows));
        while (!string.IsNullOrWhiteSpace(url) && result.Count < wanted && result.Count < MaximumProviderRows)
        {
            using var document = await GetJsonAsync(token, url, cancellationToken);
            foreach (var element in ReadValues(document.RootElement))
            {
                if (TryMapUser(element, out var user)) result.Add(user);
                if (result.Count >= wanted || result.Count >= MaximumProviderRows) break;
            }
            url = ReadNextLink(document.RootElement);
        }
        return result;
    }

    private async Task<List<DirectoryGroupRecord>> GetGroupCollectionAsync(string token, string? url, int wanted, CancellationToken cancellationToken)
    {
        var result = new List<DirectoryGroupRecord>(Math.Min(wanted, MaximumProviderRows));
        while (!string.IsNullOrWhiteSpace(url) && result.Count < wanted && result.Count < MaximumProviderRows)
        {
            using var document = await GetJsonAsync(token, url, cancellationToken);
            foreach (var element in ReadValues(document.RootElement))
            {
                result.Add(MapGroup(element));
                if (result.Count >= wanted || result.Count >= MaximumProviderRows) break;
            }
            url = ReadNextLink(document.RootElement);
        }
        return result;
    }

    private async Task<string> AcquireTokenAsync(DirectoryConnection connection, string? credential, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(connection.DirectoryTenantId, out _) || !Guid.TryParse(connection.ClientId, out _) || string.IsNullOrWhiteSpace(credential))
            throw new DirectoryProviderException("ENTRA_CONFIGURATION_INCOMPLETE", "Set a valid Entra tenant ID and client ID, then configure the referenced API credential.");

        var app = ConfidentialClientApplicationBuilder.Create(connection.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, connection.DirectoryTenantId)
            .WithClientSecret(credential)
            .Build();
        var token = await app.AcquireTokenForClient(["https://graph.microsoft.com/.default"]).ExecuteAsync(cancellationToken);
        return token.AccessToken;
    }

    private async Task<JsonDocument> GetJsonAsync(string token, string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith("/v1.0/", StringComparison.Ordinal))
            throw new DirectoryProviderException("ENTRA_INVALID_PAGE", "Microsoft Graph returned an invalid paging URL.");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClientFactory.CreateClient(DirectoryProviderHttpClients.Entra).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var code = await ReadGraphErrorCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new DirectoryProviderException("ENTRA_PERMISSION_MISSING", "Microsoft Graph denied this read. Grant admin consent for User.Read.All and GroupMember.Read.All, then test again.", response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new DirectoryProviderException("ENTRA_AUTH_FAILED", "Entra rejected the configured credential. Verify the tenant, client ID, and server-side credential reference.", response.StatusCode);
            throw new DirectoryProviderException(code ?? "ENTRA_GRAPH_ERROR", $"Microsoft Graph returned HTTP {(int)response.StatusCode}.", response.StatusCode);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task<string?> ReadGraphErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<JsonElement> ReadValues(JsonElement root) =>
        root.TryGetProperty("value", out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray() : [];

    private static string? ReadNextLink(JsonElement root)
    {
        if (!root.TryGetProperty("@odata.nextLink", out var next)) return null;
        return next.ValueKind == JsonValueKind.String ? next.GetString() : null;
    }

    private static bool TryMapUser(JsonElement element, out DirectoryUserRecord user)
    {
        user = null!;
        if (!element.TryGetProperty("id", out var idValue) || string.IsNullOrWhiteSpace(idValue.GetString())) return false;
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = idValue.GetString(),
            ["userPrincipalName"] = ReadString(element, "userPrincipalName"),
            ["displayName"] = ReadString(element, "displayName"),
            ["givenName"] = ReadString(element, "givenName"),
            ["surname"] = ReadString(element, "surname"),
            ["mail"] = ReadString(element, "mail"),
            ["department"] = ReadString(element, "department"),
            ["jobTitle"] = ReadString(element, "jobTitle"),
            ["employeeId"] = ReadString(element, "employeeId"),
            ["manager"] = ReadManagerId(element),
            ["accountEnabled"] = element.TryGetProperty("accountEnabled", out var enabled) ? enabled.ToString() : "true"
        };
        user = new DirectoryUserRecord(
            idValue.GetString()!, attributes["userPrincipalName"], attributes["userPrincipalName"], attributes["displayName"],
            attributes["mail"], attributes["department"], attributes["jobTitle"], attributes["employeeId"],
            ReadManagerId(element), !string.Equals(attributes["accountEnabled"], "false", StringComparison.OrdinalIgnoreCase), [], attributes);
        return true;
    }

    private static string? ReadManagerId(JsonElement element) =>
        element.TryGetProperty("manager", out var manager) && manager.ValueKind == JsonValueKind.Object
            ? ReadString(manager, "id")
            : null;

    private static DirectoryGroupRecord MapGroup(JsonElement element)
    {
        var id = ReadString(element, "id") ?? string.Empty;
        var name = ReadString(element, "displayName") ?? id;
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id,
            ["displayName"] = name,
            ["description"] = ReadString(element, "description")
        };
        return new DirectoryGroupRecord(id, name, attributes["description"], true, attributes);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? value.ToString() : null;

    private static string[] ReadIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions)?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        }
        catch (JsonException) { return []; }
    }

    private static bool MatchesConfiguredFilter(DirectoryUserRecord user, string filter)
    {
        // Directory-specific filters are applied by the provider query builder; this
        // additional local filter accepts only a simple `attribute=value` expression.
        var separator = filter.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0) return false;
        var key = filter[..separator].Trim();
        var value = filter[(separator + 1)..].Trim();
        return user.Attributes.TryGetValue(key, out var actual) && string.Equals(actual, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
