using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NEXUS.Api.Security;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ForwardedHeadersTrustTests
{
    [Theory]
    [InlineData("198.51.100.25")]
    [InlineData("10.20.30.40")]
    public async Task Untrusted_proxy_cannot_set_request_scheme_from_forwarded_proto(string remoteAddress)
    {
        var forwardedHeaders = CreateForwardedHeadersOptions();
        var observedScheme = string.Empty;
        var middleware = CreateMiddleware(forwardedHeaders, context =>
        {
            observedScheme = context.Request.Scheme;
            return Task.CompletedTask;
        });

        var context = ForwardedRequest(remoteAddress);
        await middleware.Invoke(context);

        Assert.Equal("http", observedScheme);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Explicit_loopback_proxy_can_forward_https_scheme(string remoteAddress)
    {
        var forwardedHeaders = CreateForwardedHeadersOptions();
        var middleware = CreateMiddleware(forwardedHeaders, _ => Task.CompletedTask);

        var context = ForwardedRequest(remoteAddress);
        await middleware.Invoke(context);

        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public void Policy_uses_supported_ip_network_collection_and_keeps_trust_allowlist_exact()
    {
        var options = CreateForwardedHeadersOptions();

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }, options.KnownProxies);
        Assert.Equal(2, options.ForwardLimit);
    }

    private static ForwardedHeadersOptions CreateForwardedHeadersOptions()
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSecurityPolicy.Configure(options);
        return options;
    }

    private static ForwardedHeadersMiddleware CreateMiddleware(
        ForwardedHeadersOptions options,
        RequestDelegate next)
    {
        return new ForwardedHeadersMiddleware(next, NullLoggerFactory.Instance, Options.Create(options));
    }

    private static DefaultHttpContext ForwardedRequest(string remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.17";
        return context;
    }
}
