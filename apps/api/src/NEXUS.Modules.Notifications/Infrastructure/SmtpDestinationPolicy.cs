using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>
/// Deployment-owned egress allowlists. These values must never be populated from a tenant or
/// organization SMTP profile. A scoped rule wins over broader rules so one tenant cannot borrow
/// another tenant's private relay authorization.
/// </summary>
public sealed class SmtpDestinationPolicyOptions
{
    public List<SmtpDestinationAllowlistRule> Rules { get; set; } = [];
}

public sealed class SmtpDestinationAllowlistRule
{
    public Guid? TenantId { get; set; }
    public Guid? OrganizationId { get; set; }
    public List<string> PrivateRelayHosts { get; set; } = [];
    public List<string> PrivateRelayNetworks { get; set; } = [];
    public List<string> TrustedRelayHosts { get; set; } = [];
    public List<string> TrustedRelayNetworks { get; set; } = [];
}

/// <summary>
/// Blocks loopback, private, link-local, reserved, and special-use addresses by default. Private
/// SMTP targets and unauthenticated relays require both an exact host entry and a scoped CIDR
/// entry supplied by deployment configuration.
/// </summary>
public sealed class SmtpDestinationPolicy : ISmtpDestinationPolicy
{
    private readonly SmtpDestinationPolicyOptions _options;

    public SmtpDestinationPolicy(IOptions<SmtpDestinationPolicyOptions> options) => _options = options.Value;

    public bool IsDestinationAllowed(
        Guid? tenantId,
        Guid? organizationId,
        string normalizedHost,
        IPAddress resolvedAddress,
        SmtpAuthenticationMode authenticationMode)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty || (tenantId is null && organizationId is not null))
            return false;

        var address = NormalizeAddress(resolvedAddress);
        var host = NormalizeHost(normalizedHost);
        if (host is null || address is null)
            return false;

        if (authenticationMode == SmtpAuthenticationMode.OAuth2)
            return false;

        var scopedRules = GetMostSpecificRules(tenantId, organizationId);
        if (authenticationMode == SmtpAuthenticationMode.TrustedRelay)
        {
            // Trusted Relay is only safe inside explicitly authorized RFC1918/ULA networks.
            // Loopback, link-local, shared, multicast, and other special-use addresses remain
            // blocked even when an operator accidentally configures a broad CIDR.
            return IsPrivateInternalAddress(address)
                && IsAllowedByRule(scopedRules, host, address, trustedRelay: true);
        }

        return IsPubliclyRoutable(address)
            || IsPrivateInternalAddress(address) && IsAllowedByRule(scopedRules, host, address, trustedRelay: false);
    }

    public bool IsTrustedRelayHostAuthorized(Guid? tenantId, Guid? organizationId, string host)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty || (tenantId is null && organizationId is not null))
            return false;

        var normalizedHost = NormalizeHost(host);
        if (normalizedHost is null)
            return false;

        return GetMostSpecificRules(tenantId, organizationId).Any(rule =>
            ContainsHost(rule.TrustedRelayHosts, normalizedHost)
            && rule.TrustedRelayNetworks.Any(IsAllowedPrivateNetwork));
    }

    private IReadOnlyList<SmtpDestinationAllowlistRule> GetMostSpecificRules(Guid? tenantId, Guid? organizationId)
    {
        var rules = _options.Rules;
        if (tenantId is Guid tenant && organizationId is Guid orgId)
        {
            var organizationRules = rules
                .Where(rule => rule.TenantId == tenant && rule.OrganizationId == orgId)
                .ToArray();
            if (organizationRules.Length > 0)
                return organizationRules;
        }

        if (tenantId is Guid tenantScope)
        {
            var tenantRules = rules
                .Where(rule => rule.TenantId == tenantScope && rule.OrganizationId is null)
                .ToArray();
            if (tenantRules.Length > 0)
                return tenantRules;
        }

        return rules
            .Where(rule => rule.TenantId is null && rule.OrganizationId is null)
            .ToArray();
    }

    private static bool IsAllowedByRule(
        IReadOnlyList<SmtpDestinationAllowlistRule> rules,
        string host,
        IPAddress address,
        bool trustedRelay) => rules.Any(rule =>
        {
            var hosts = trustedRelay ? rule.TrustedRelayHosts : rule.PrivateRelayHosts;
            var networks = trustedRelay ? rule.TrustedRelayNetworks : rule.PrivateRelayNetworks;
            return ContainsHost(hosts, host)
                && networks.Any(cidr => IsAllowedPrivateNetwork(cidr) && IpCidr.Contains(cidr, address));
        });

    private static bool IsAllowedPrivateNetwork(string cidr)
    {
        if (!IpCidr.TryParse(cidr, out var network, out var prefixLength))
            return false;

        if (network.AddressFamily == AddressFamily.InterNetwork)
        {
            return prefixLength >= 8 && IpCidr.Contains("10.0.0.0/8", network)
                || prefixLength >= 12 && IpCidr.Contains("172.16.0.0/12", network)
                || prefixLength >= 16 && IpCidr.Contains("192.168.0.0/16", network);
        }

        return network.AddressFamily == AddressFamily.InterNetworkV6
            && prefixLength >= 7 && IpCidr.Contains("fc00::/7", network);
    }

    private static bool IsPrivateInternalAddress(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
            ? IsInAny(address, ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"])
            : address.AddressFamily == AddressFamily.InterNetworkV6 && IpCidr.Contains("fc00::/7", address);

    private static bool ContainsHost(IEnumerable<string> hosts, string normalizedHost) => hosts
        .Select(NormalizeHost)
        .Any(host => host is not null && string.Equals(host, normalizedHost, StringComparison.Ordinal));

    internal static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host.Any(char.IsWhiteSpace))
            return null;

        var value = host.Trim().TrimEnd('.');
        if (IPAddress.TryParse(value, out var address))
            return NormalizeAddress(address)?.ToString().ToLowerInvariant();

        try
        {
            var ascii = new IdnMapping().GetAscii(value);
            return Uri.CheckHostName(ascii) == UriHostNameType.Dns
                ? ascii.ToLowerInvariant()
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IPAddress? NormalizeAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();
        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return null;
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
            return null;
        return address;
    }

    private static bool IsPubliclyRoutable(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !IsInAny(address,
            [
                "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16",
                "172.16.0.0/12", "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16",
                "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/4", "240.0.0.0/4",
            ]);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Publicly routable unicast IPv6 currently lives in 2000::/3. This excludes ULA,
            // link-local, multicast, unspecified, loopback, documentation, transition, and other
            // special-use ranges without relying on platform-specific IsIPv6SiteLocal behavior.
            return IpCidr.Contains("2000::/3", address)
                && !IsInAny(address, ["2001:db8::/32", "2001::/23", "2002::/16"]);
        }

        return false;
    }

    private static bool IsInAny(IPAddress address, IEnumerable<string> cidrs) =>
        cidrs.Any(cidr => IpCidr.Contains(cidr, address));

    private static class IpCidr
    {
        public static bool TryParse(string value) => TryParseInternal(value, out _, out _);

        public static bool TryParse(string value, out IPAddress address, out int prefixLength) =>
            TryParseInternal(value, out address, out prefixLength);

        public static bool Contains(string value, IPAddress address)
        {
            if (!TryParseInternal(value, out var network, out var prefixLength))
                return false;

            address = NormalizeAddress(address) ?? address;
            if (network.AddressFamily != address.AddressFamily)
                return false;

            var networkBytes = network.GetAddressBytes();
            var addressBytes = address.GetAddressBytes();
            var wholeBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            for (var index = 0; index < wholeBytes; index++)
            {
                if (networkBytes[index] != addressBytes[index])
                    return false;
            }

            if (remainingBits == 0)
                return true;

            var mask = (byte)(0xff << (8 - remainingBits));
            return (networkBytes[wholeBytes] & mask) == (addressBytes[wholeBytes] & mask);
        }

        private static bool TryParseInternal(string value, out IPAddress address, out int prefixLength)
        {
            address = IPAddress.None;
            prefixLength = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var parsed)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
                return false;

            parsed = NormalizeAddress(parsed) ?? parsed;
            var bits = parsed.AddressFamily == AddressFamily.InterNetwork ? 32
                : parsed.AddressFamily == AddressFamily.InterNetworkV6 ? 128
                : 0;
            if (bits == 0 || prefix < 0 || prefix > bits)
                return false;

            address = parsed;
            prefixLength = prefix;
            return true;
        }
    }
}
