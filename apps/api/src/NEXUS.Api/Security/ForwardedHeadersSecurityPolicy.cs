using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NEXUS.Api.Security;

/// <summary>
/// Configures the proxy trust boundary for forwarded client and protocol headers.
/// Only the two loopback proxy addresses are trusted; no IP network ranges are
/// implicitly trusted.
/// </summary>
public static class ForwardedHeadersSecurityPolicy
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        ICollection<System.Net.IPNetwork> knownIpNetworks = options.KnownIPNetworks;
        knownIpNetworks.Clear();

        options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
        options.ForwardLimit = 2;
    }
}
