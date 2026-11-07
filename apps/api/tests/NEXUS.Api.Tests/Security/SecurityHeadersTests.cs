using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NEXUS.Api.Middleware;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for the SecurityHeadersMiddleware: every response
/// must carry restrictive security headers, and non-Development environments
/// must additionally enforce Strict-Transport-Security.
/// </summary>
public sealed class SecurityHeadersTests
{
    [Fact]
    public async Task Development_response_includes_core_security_headers_without_hsts()
    {
        var middleware = new SecurityHeadersMiddleware(
            (innerContext) => Task.CompletedTask,
            new HostingEnvironment { EnvironmentName = "Development" });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Content-Type"] = "application/json";

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task Production_response_includes_core_security_headers_with_hsts()
    {
        var middleware = new SecurityHeadersMiddleware(
            (innerContext) => Task.CompletedTask,
            new HostingEnvironment { EnvironmentName = "Production" });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Content-Type"] = "application/json";

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers["Strict-Transport-Security"]);
        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task Html_response_includes_content_security_policy_in_production()
    {
        var middleware = new SecurityHeadersMiddleware(
            (innerContext) => Task.CompletedTask,
            new HostingEnvironment { EnvironmentName = "Production" });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Content-Type"] = "text/html";

        await middleware.InvokeAsync(context);

        var csp = context.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("default-src 'none'", csp);
    }

    [Fact]
    public async Task Development_html_response_includes_relaxed_csp()
    {
        var middleware = new SecurityHeadersMiddleware(
            (innerContext) => Task.CompletedTask,
            new HostingEnvironment { EnvironmentName = "Development" });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Content-Type"] = "text/html";

        await middleware.InvokeAsync(context);

        var csp = context.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("http://localhost:*", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    private sealed class HostingEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => new NullDirectoryContents();
        public IFileInfo GetFileInfo(string subpath) => new NullFileInfo();
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class NullDirectoryContents : IDirectoryContents
    {
        public bool Exists => false;
        public IEnumerator<IFileInfo> GetEnumerator() => Enumerate().GetEnumerator();
        private static IEnumerable<IFileInfo> Enumerate() => Enumerable.Empty<IFileInfo>();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();
    }

    private sealed class NullFileInfo : IFileInfo
    {
        public bool Exists => false;
        public bool IsDirectory => false;
        public DateTimeOffset LastModified => default;
        public long Length => 0;
        public string Name => string.Empty;
        public string PhysicalPath => string.Empty;
        public Stream CreateReadStream() => Stream.Null;
    }
}
