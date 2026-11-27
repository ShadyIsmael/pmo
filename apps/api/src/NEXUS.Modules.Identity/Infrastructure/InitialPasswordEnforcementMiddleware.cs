using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class InitialPasswordEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public InitialPasswordEnforcementMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        DbContext dbContext,
        IHostEnvironment environment,
        IOptionsMonitor<LocalPasswordPolicyOptions> passwordPolicy)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // A trusted platform-only token can provision the first tenant without an
        // application-tenant account. Tenant APIs remain closed because the
        // permission middleware strips any malformed tenant context downstream.
        if (HasPlatformAuthority(context.User)
            && !Guid.TryParse(context.User.FindFirstValue("tenantId"), out _))
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "The authenticated account context is not available.");
            return;
        }

        var state = await dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == userId && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => new
            {
                user.Email,
                user.AuthenticationSource,
                user.IsActive,
                user.MustChangePassword,
                user.TemporaryPasswordExpiresAt,
                user.PasswordChangedAt
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (state is null || !state.IsActive)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "The account is not available.");
            return;
        }

        var tokenPasswordChangedAt = context.User.FindFirstValue("pwdts");
        var expectedVersion = state.PasswordChangedAt?.Ticks ?? 0;
        // A token is "legacy unversioned" when the DB has no PasswordChangedAt
        // AND the token either lacks the pwdts claim entirely or has pwdts="0".
        var isLegacyUnversionedToken = expectedVersion == 0
                                         && (tokenPasswordChangedAt is null
                                             || tokenPasswordChangedAt == "0");
        if (!isLegacyUnversionedToken
            && (!long.TryParse(tokenPasswordChangedAt, NumberStyles.None, CultureInfo.InvariantCulture, out var tokenVersion)
                || tokenVersion != expectedVersion))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "CREDENTIALS_INVALIDATED", "Sign in again to continue.");
            return;
        }

        var bypassInitialPasswordPolicy = DevelopmentAdminTemporaryPasswordBypass.IsEnabled(
            tenantId,
            state.Email,
            state.AuthenticationSource,
            passwordPolicy.CurrentValue,
            environment);
        if (state.MustChangePassword && !bypassInitialPasswordPolicy && !IsAllowedDuringPasswordChange(context.Request))
        {
            var expired = !state.TemporaryPasswordExpiresAt.HasValue || state.TemporaryPasswordExpiresAt <= DateTime.UtcNow;
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                expired ? "TEMPORARY_PASSWORD_EXPIRED" : "PASSWORD_CHANGE_REQUIRED",
                expired
                    ? "The temporary password has expired. Contact an administrator to generate a new one."
                    : "Change the temporary password before using NEXUS.");
            return;
        }

        await _next(context);
    }

    private static bool IsAllowedDuringPasswordChange(HttpRequest request)
    {
        var path = request.Path;
        return (HttpMethods.IsGet(request.Method) && string.Equals(path.Value, "/api/auth/me", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsGet(request.Method) && string.Equals(path.Value, "/api/auth/initial-password-policy", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsPost(request.Method)
                && (string.Equals(path.Value, "/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path.Value, "/api/auth/change-initial-password", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasPlatformAuthority(ClaimsPrincipal principal) =>
        principal.FindAll("platform_role").Any(claim =>
            string.Equals(claim.Value, "PlatformAdministrator", StringComparison.OrdinalIgnoreCase))
        || principal.FindAll("platform_permission").Any(claim =>
            claim.Value is "tenants.manage");

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Password change required",
            Detail = detail
        };
        problem.Extensions["code"] = code;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
