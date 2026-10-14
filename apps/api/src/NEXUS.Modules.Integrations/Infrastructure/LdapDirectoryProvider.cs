using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Domain;
using DirectoryConnection = NEXUS.Modules.Integrations.Domain.DirectoryConnection;

namespace NEXUS.Modules.Integrations.Infrastructure;

public sealed class LdapDirectoryProvider : IDirectoryProvider
{
    private const int MaximumProviderRows = 5001;
    private const int MaximumPageSize = 5000;
    private const int LdapPageSize = 1000;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private readonly HashSet<string> _allowedHosts;

    public LdapDirectoryProvider(IOptions<DirectoryLdapOptions> options)
    {
        _allowedHosts = options.Value.AllowedHosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(NormalizeHost)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string ProviderType => DirectoryProviderTypes.Ldap;

    public Task<DirectoryProviderTestResult> TestConnectionAsync(DirectoryConnection connection, string? credential, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var ldap = Connect(connection, credential);
            var request = new SearchRequest(connection.BaseDn, "(objectClass=*)", SearchScope.Base, "distinguishedName");
            request.SizeLimit = 1;
            _ = (SearchResponse)ldap.SendRequest(request);
            timer.Stop();
            return Task.FromResult(new DirectoryProviderTestResult(true, ProviderType, connection.BaseDn, timer.ElapsedMilliseconds, null, "Secure LDAP bind and directory read succeeded."));
        }
        catch (DirectoryProviderException exception)
        {
            timer.Stop();
            return Task.FromResult(new DirectoryProviderTestResult(false, ProviderType, null, timer.ElapsedMilliseconds, exception.Code, exception.Message));
        }
        catch (LdapException exception)
        {
            timer.Stop();
            var permissionDenied = exception.ErrorCode is 50 or 32;
            var code = permissionDenied ? "LDAP_READ_PERMISSION_MISSING" : "LDAP_CONNECTION_FAILED";
            var message = permissionDenied
                ? "The LDAP bind succeeded but cannot read the configured Base DN. Verify the bind account and search permissions."
                : "The secure LDAP connection or bind failed. Verify host, port, certificate trust, bind identity, credential reference, and Base DN.";
            return Task.FromResult(new DirectoryProviderTestResult(false, ProviderType, null, timer.ElapsedMilliseconds, code, message));
        }
        catch (SocketException)
        {
            timer.Stop();
            return Task.FromResult(new DirectoryProviderTestResult(false, ProviderType, null, timer.ElapsedMilliseconds, "LDAP_UNREACHABLE", "The configured secure LDAP host could not be reached from the API host."));
        }
    }

    public Task<DirectoryProviderPage<DirectoryUserRecord>> GetUsersAsync(DirectoryConnection connection, DirectorySyncScope scope, string? credential, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var ldap = Connect(connection, credential);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
        var maxItems = Math.Clamp(scope.MaxItems + 1, 2, MaximumProviderRows);
        var skip = (page - 1) * pageSize;
        if (skip >= maxItems) return Task.FromResult(new DirectoryProviderPage<DirectoryUserRecord>([], page, pageSize, false));

        var selectedGroupIds = scope.ScopeType == DirectoryScopeTypes.SelectedGroups ? ReadIds(scope.SelectedGroupIdsJson) : [];
        var selectedGroups = selectedGroupIds.Length == 0 ? [] : ResolveSelectedGroups(ldap, connection.BaseDn!, selectedGroupIds, cancellationToken);
        var selectedGroupDns = selectedGroups.Select(group => group.Attributes.GetValueOrDefault("distinguishedName"))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
        var filter = BuildUserFilter(scope, selectedGroupDns);
        var bases = GetUserSearchBases(connection, scope);
        var candidates = new List<SearchResultEntry>();
        var wanted = Math.Min(maxItems, skip + pageSize + 1);
        foreach (var searchBase in bases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = SearchPaged(ldap, searchBase, filter, ["objectGUID", "distinguishedName", "userPrincipalName", "sAMAccountName", "displayName", "givenName", "sn", "mail", "department", "title", "employeeID", "manager", "memberOf", "userAccountControl"], 0, wanted, maxItems, cancellationToken);
            candidates.AddRange(entries);
            if (candidates.Count >= wanted) break;
        }

        var records = candidates
            .Select(MapUser)
            .Where(record => record is not null)
            .Cast<DirectoryUserRecord>()
            .Select(record => scope.ScopeType == DirectoryScopeTypes.SelectedGroups
                ? record with
                {
                    GroupIds = selectedGroups.Where(group => group.Attributes.TryGetValue("distinguishedName", out var dn)
                        && !string.IsNullOrWhiteSpace(dn) && record.GroupIds.Contains(dn, StringComparer.OrdinalIgnoreCase))
                        .Select(group => group.ExternalDirectoryId).ToArray(),
                    GroupMembershipsLoaded = true
                }
                : record)
            .DistinctBy(record => record.ExternalDirectoryId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(record => Contains(record.DisplayName, search) || Contains(record.Email, search) || Contains(record.UserPrincipalName, search)).ToArray();
        }
        return Task.FromResult(new DirectoryProviderPage<DirectoryUserRecord>(records.Skip(skip).Take(pageSize).ToArray(), page, pageSize, records.Length > skip + pageSize));
    }

    public Task<DirectoryProviderPage<DirectoryGroupRecord>> GetGroupsAsync(DirectoryConnection connection, string? credential, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var ldap = Connect(connection, credential);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
        var skip = (page - 1) * pageSize;
        var search = string.IsNullOrWhiteSpace(query.Search) ? string.Empty : $"(cn=*{EscapeFilter(query.Search.Trim())}*)";
        var filter = $"(&(objectCategory=group)(objectClass=group){search})";
        var entries = SearchPaged(ldap, connection.BaseDn!, filter, ["objectGUID", "distinguishedName", "cn", "description"], skip, pageSize + 1, MaximumProviderRows, cancellationToken);
        var records = entries.Select(MapGroup).Where(group => group is not null).Cast<DirectoryGroupRecord>().ToArray();
        return Task.FromResult(new DirectoryProviderPage<DirectoryGroupRecord>(records.Take(pageSize).ToArray(), page, pageSize, records.Length > pageSize));
    }

    private LdapConnection Connect(DirectoryConnection config, string? credential)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.BaseDn)
            || string.IsNullOrWhiteSpace(config.BindUser) || string.IsNullOrWhiteSpace(credential))
            throw new DirectoryProviderException("LDAP_CONFIGURATION_INCOMPLETE", "Set a host, Base DN, bind user, and server-side credential reference before testing LDAP.");
        if (!config.UseSsl || config.Port is not (636 or 3269))
            throw new DirectoryProviderException("LDAP_TLS_REQUIRED", "Directory Integration requires LDAPS on port 636 or 3269; unencrypted LDAP binds are not allowed.");
        if (!IsValidHost(config.Host))
            throw new DirectoryProviderException("LDAP_HOST_INVALID", "Enter a valid LDAP host name or IP address without a URL scheme or path.");
        if (!_allowedHosts.Contains(NormalizeHost(config.Host)))
            throw new DirectoryProviderException("LDAP_HOST_NOT_ALLOWLISTED", "The configured LDAP host is not allowlisted by the API administrator.");
        if (!IsValidBaseDn(config.BaseDn))
            throw new DirectoryProviderException("LDAP_BASE_DN_INVALID", "Enter a valid Base DN for the configured directory.");

        var identifier = new LdapDirectoryIdentifier(config.Host, config.Port.Value, fullyQualifiedDnsHostName: true, connectionless: false);
        var connection = new LdapConnection(identifier, new NetworkCredential(config.BindUser, credential), AuthType.Basic)
        {
            Timeout = _timeout,
            AuthType = AuthType.Basic
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;
        try
        {
            connection.Bind();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<DirectoryGroupRecord> ResolveSelectedGroups(LdapConnection connection, string baseDn, IReadOnlyList<string> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count > 50) throw new DirectoryProviderException("LDAP_SCOPE_TOO_LARGE", "Select at most 50 groups for one directory preview.");
        var filter = $"(&(objectCategory=group)(objectClass=group){BuildSelectedUsersFilter(groupIds)})";
        var entries = SearchPaged(connection, baseDn, filter, ["objectGUID", "distinguishedName", "cn", "description"], 0, groupIds.Count, Math.Min(groupIds.Count + 1, MaximumProviderRows), cancellationToken);
        return entries.Select(MapGroup).Where(group => group is not null).Cast<DirectoryGroupRecord>().ToArray();
    }

    private static string BuildUserFilter(DirectorySyncScope scope, IReadOnlyList<string> selectedGroupDns)
    {
        const string personFilter = "(&(objectCategory=person)(objectClass=user)(!(objectClass=computer))";
        var configured = NormalizeConfiguredFilter(scope.UserFilter);
        var extra = string.IsNullOrEmpty(configured) ? string.Empty : configured;
        return scope.ScopeType switch
        {
            DirectoryScopeTypes.SelectedGroups => JoinFilter(personFilter, BuildMemberOfFilter(selectedGroupDns), extra),
            DirectoryScopeTypes.SelectedUsers => JoinFilter(personFilter, BuildSelectedUsersFilter(ReadIds(scope.SelectedUserIdsJson)), extra),
            DirectoryScopeTypes.OrganizationalUnits => JoinFilter(personFilter, string.Empty, extra),
            DirectoryScopeTypes.AllUsers => JoinFilter(personFilter, string.Empty, extra),
            _ => "(&(objectCategory=person)(objectClass=user)(!(objectClass=computer))(!(objectClass=*))"
        };
    }

    private static string JoinFilter(string opening, string scoped, string extra) => $"{opening}{scoped}{extra})";

    private static string BuildMemberOfFilter(IReadOnlyList<string> groupDns)
    {
        if (groupDns.Count == 0) return "(!(objectClass=*))";
        if (groupDns.Count > 50) throw new DirectoryProviderException("LDAP_SCOPE_TOO_LARGE", "Select at most 50 groups for one directory preview.");
        return $"(|{string.Concat(groupDns.Select(dn => $"(memberOf={EscapeFilter(dn)})"))})";
    }

    private static string BuildSelectedUsersFilter(IReadOnlyList<string> identifiers)
    {
        if (identifiers.Count == 0) return "(!(objectClass=*))";
        if (identifiers.Count > 1000) throw new DirectoryProviderException("LDAP_SCOPE_TOO_LARGE", "Select at most 1000 users for one directory preview.");
        return $"(|{string.Concat(identifiers.Select(identifier => Guid.TryParse(identifier, out var guid)
            ? $"(objectGUID={EscapeGuid(guid)})"
            : $"(|(userPrincipalName={EscapeFilter(identifier)})(sAMAccountName={EscapeFilter(identifier)}))"))})";
    }

    private static IReadOnlyList<string> GetUserSearchBases(DirectoryConnection config, DirectorySyncScope scope)
    {
        if (scope.ScopeType != DirectoryScopeTypes.OrganizationalUnits) return [config.BaseDn!];
        var bases = ReadIds(scope.OrganizationalUnitsJson)
            .Where(value => IsWithinBaseDn(value, config.BaseDn!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();
        if (bases.Length == 0) throw new DirectoryProviderException("LDAP_SCOPE_EMPTY", "Choose one or more OUs under the configured Base DN.");
        return bases;
    }

    private static IReadOnlyList<SearchResultEntry> SearchPaged(LdapConnection connection, string baseDn, string filter, string[] attributes, int skip, int take, int maxItems, CancellationToken cancellationToken)
    {
        var results = new List<SearchResultEntry>();
        byte[] cookie = [];
        var ordinal = 0;
        var absoluteLimit = Math.Min(maxItems, MaximumProviderRows);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new SearchRequest(baseDn, filter, SearchScope.Subtree, attributes)
            {
                SizeLimit = LdapPageSize
            };
            var pageControl = new PageResultRequestControl(LdapPageSize) { Cookie = cookie };
            request.Controls.Add(pageControl);
            var response = (SearchResponse)connection.SendRequest(request);
            foreach (SearchResultEntry entry in response.Entries)
            {
                if (ordinal >= skip && results.Count < take) results.Add(entry);
                ordinal++;
                if (results.Count >= take || ordinal >= absoluteLimit) return results;
            }
            cookie = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault()?.Cookie ?? [];
        } while (cookie.Length > 0 && results.Count < skip + take && results.Count < absoluteLimit);
        return results;
    }

    private static DirectoryUserRecord? MapUser(SearchResultEntry entry)
    {
        var externalId = ReadGuid(entry, "objectGUID")?.ToString("D");
        if (string.IsNullOrWhiteSpace(externalId)) return null;
        var dn = ReadString(entry, "distinguishedName");
        var upn = ReadString(entry, "userPrincipalName");
        var username = ReadString(entry, "sAMAccountName") ?? upn;
        var displayName = ReadString(entry, "displayName");
        var firstName = ReadString(entry, "givenName");
        var lastName = ReadString(entry, "sn");
        var email = ReadString(entry, "mail");
        var department = ReadString(entry, "department");
        var title = ReadString(entry, "title");
        var employeeId = ReadString(entry, "employeeID");
        var managerDn = ReadString(entry, "manager");
        var userControl = int.TryParse(ReadString(entry, "userAccountControl"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["objectGUID"] = externalId,
            ["distinguishedName"] = dn,
            ["userPrincipalName"] = upn,
            ["sAMAccountName"] = username,
            ["displayName"] = displayName,
            ["givenName"] = firstName,
            ["sn"] = lastName,
            ["mail"] = email,
            ["department"] = department,
            ["title"] = title,
            ["employeeID"] = employeeId,
            ["manager"] = managerDn,
            ["accountEnabled"] = ((userControl & 2) == 0).ToString().ToLowerInvariant()
        };
        var groups = ReadStrings(entry, "memberOf");
        return new DirectoryUserRecord(externalId, upn, username, displayName, email, department, title, employeeId, managerDn, (userControl & 2) == 0, groups, attributes, dn);
    }

    private static DirectoryGroupRecord? MapGroup(SearchResultEntry entry)
    {
        // Keep objectGUID as the stable external ID; memberOf contains DNs and is
        // resolved back to this ID only for a selected-group scope.
        var externalId = ReadGuid(entry, "objectGUID")?.ToString("D");
        var dn = ReadString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(dn)) return null;
        var cn = ReadString(entry, "cn") ?? dn;
        var description = ReadString(entry, "description");
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["distinguishedName"] = dn,
            ["cn"] = cn,
            ["description"] = description
        };
        return new DirectoryGroupRecord(externalId, cn, description, true, attributes);
    }

    private static Guid? ReadGuid(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute) || entry.Attributes[attribute].Count == 0 || entry.Attributes[attribute][0] is not byte[] bytes || bytes.Length != 16) return null;
        return new Guid(bytes);
    }

    private static string? ReadString(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute) || entry.Attributes[attribute].Count == 0) return null;
        var value = entry.Attributes[attribute][0];
        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> ReadStrings(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute)) return [];
        var values = new List<string>();
        foreach (var value in entry.Attributes[attribute])
        {
            var text = value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text)) values.Add(text);
        }
        return values;
    }

    private static string? NormalizeConfiguredFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;
        var value = filter.Trim();
        if (value.Length > 1024 || value[0] != '(' || value[^1] != ')' || value.Any(char.IsControl))
            throw new DirectoryProviderException("LDAP_FILTER_INVALID", "LDAP filters must be a valid parenthesized filter of at most 1024 characters.");
        return value;
    }

    private static bool IsValidHost(string host)
    {
        if (host.Length > 255 || host.Contains('/') || host.Contains('\\') || host.Contains(':') && !IPAddress.TryParse(host, out _)) return false;
        return IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.');

    private static bool IsValidBaseDn(string baseDn) => baseDn.Length <= 1024 && !baseDn.Any(char.IsControl);

    private static bool IsWithinBaseDn(string candidate, string baseDn) =>
        string.Equals(candidate, baseDn, StringComparison.OrdinalIgnoreCase)
        || candidate.EndsWith("," + baseDn, StringComparison.OrdinalIgnoreCase);

    private static string EscapeFilter(string value) => value
        .Replace("\\", "\\5c", StringComparison.Ordinal)
        .Replace("*", "\\2a", StringComparison.Ordinal)
        .Replace("(", "\\28", StringComparison.Ordinal)
        .Replace(")", "\\29", StringComparison.Ordinal)
        .Replace("\0", "\\00", StringComparison.Ordinal);

    private static string EscapeGuid(Guid guid) => string.Concat(guid.ToByteArray().Select(value => $"\\{value:X2}"));

    private static string[] ReadIds(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json)?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? []; }
        catch (JsonException) { return []; }
    }

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
