# Forwarded Header Trust Policy

The API accepts `X-Forwarded-For` and `X-Forwarded-Proto` only when the immediate remote peer is explicitly trusted by `ForwardedHeadersSecurityPolicy`.

The current allowlist contains the exact loopback proxy addresses `127.0.0.1` and `::1`, with a forwarding limit of two. `KnownIPNetworks` is cleared so the framework's default network ranges cannot silently widen that trust boundary. No CIDR ranges are currently trusted. Any future network range must be added through an explicit, reviewed `System.Net.IPNetwork` entry.

`UseForwardedHeaders()` runs before HTTPS redirection and the authentication pipeline so HTTPS termination can be recognized by the application. A direct or otherwise untrusted HTTP client cannot change `Request.Scheme` by sending `X-Forwarded-Proto: https`; the HTTPS redirection middleware therefore continues to treat that request as HTTP.

Regression coverage is in `apps/api/tests/NEXUS.Api.Tests/Security/ForwardedHeadersTrustTests.cs`: it checks both loopback proxy addresses, rejects forwarded protocol values from untrusted peers, and asserts that the allowlist has no network ranges.
