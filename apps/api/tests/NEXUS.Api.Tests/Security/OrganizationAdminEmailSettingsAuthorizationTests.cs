using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Notifications.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class OrganizationAdminEmailSettingsAuthorizationTests
{
    private static readonly Guid TenantId = Guid.Parse("b2200000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("b2200000-0000-0000-0000-000000000002");
    private static readonly Guid OtherOrganizationId = Guid.Parse("b2200000-0000-0000-0000-000000000003");
    private static readonly Guid ActorId = Guid.Parse("b2200000-0000-0000-0000-000000000004");

    [Fact]
    public async Task Organization_configuration_view_and_manage_allow_scoped_email_configuration_only()
    {
        var profiles = new Mock<IEmailNotificationProfileStore>();
        EmailNotificationProfileRecord? saved = null;
        profiles.Setup(store => store.GetExactAsync(EmailNotificationProfileScope.Organization, TenantId, OrganizationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved);
        profiles.Setup(store => store.ResolveEffectiveAsync(TenantId, OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved);
        profiles.Setup(store => store.UpsertAsync(
                EmailNotificationProfileScope.Organization, TenantId, OrganizationId,
                It.IsAny<SmtpTransportProfile>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailNotificationProfileScope scope, Guid? tenantId, Guid? organizationId,
                SmtpTransportProfile profile, string? _, CancellationToken _) =>
            {
                var record = new EmailNotificationProfileRecord(scope, tenantId, organizationId, "AQEBAQEBAQE=", profile);
                saved = record;
                return record;
            });
        var organizationAccess = ScopedOrganizationAccess();
        var service = CreateService(
            ["OrganizationConfiguration.View", "OrganizationConfiguration.Manage"],
            profiles.Object, organizationAccess.Object, out var testOperations);

        var view = await service.GetSettingsAsync(EmailNotificationProfileScope.Organization, OrganizationId);
        Assert.Equal(EmailNotificationProfileScope.Organization, view.Scope);
        Assert.True(view.CanManage);
        Assert.False(view.CanTest);
        Assert.False(view.CanViewLogs);

        var update = await service.UpdateSettingsAsync(
            EmailNotificationProfileScope.Organization,
            OrganizationId,
            new UpdateEmailNotificationProfileRequest(
                Enabled: true,
                Host: "smtp.example.test",
                Port: 587,
                SecurityMode: SmtpSecurityMode.StartTlsRequired,
                AuthenticationMode: SmtpAuthenticationMode.UsernamePassword,
                UserName: "smtp-user",
                Password: "test-only-secret",
                ClearPassword: false,
                FromAddress: "notifications@example.test",
                FromName: "FOPX Notifications",
                ReplyToAddress: null,
                ExpectedVersion: null));
        Assert.True(update.HasOverride);
        Assert.Equal(OrganizationId, update.OwnerId);
        profiles.Verify(store => store.UpsertAsync(
            EmailNotificationProfileScope.Organization, TenantId, OrganizationId,
            It.IsAny<SmtpTransportProfile>(), null, It.IsAny<CancellationToken>()), Times.Once);

        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.GetSettingsAsync(EmailNotificationProfileScope.Organization, OtherOrganizationId));
        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.TestConnectionAsync(EmailNotificationProfileScope.Organization, OrganizationId));
        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.GetLogsAsync(EmailNotificationProfileScope.Organization, OrganizationId, 25));
        testOperations.Verify(operation => operation.TestConnectionAsync(
            It.IsAny<SmtpTransportProfile>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(store => store.GetRecentDeliveryLogsAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Organization_configuration_permissions_do_not_authorize_tenant_or_platform_email_profiles()
    {
        var profiles = new Mock<IEmailNotificationProfileStore>();
        var organizationAccess = ScopedOrganizationAccess();
        var service = CreateService(
            ["OrganizationConfiguration.View", "OrganizationConfiguration.Manage"],
            profiles.Object, organizationAccess.Object, out _);
        var update = new UpdateEmailNotificationProfileRequest(
            Enabled: false,
            Host: "smtp.example.test",
            Port: 587,
            SecurityMode: SmtpSecurityMode.StartTlsRequired,
            AuthenticationMode: SmtpAuthenticationMode.UsernamePassword,
            UserName: "smtp-user",
            Password: null,
            ClearPassword: true,
            FromAddress: "notifications@example.test",
            FromName: "FOPX Notifications",
            ReplyToAddress: null,
            ExpectedVersion: null);

        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.GetSettingsAsync(EmailNotificationProfileScope.Tenant, null));
        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.UpdateSettingsAsync(EmailNotificationProfileScope.Tenant, null, update));
        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.GetSettingsAsync(EmailNotificationProfileScope.Platform, null));
        await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            service.UpdateSettingsAsync(EmailNotificationProfileScope.Platform, null, update));

        profiles.Verify(store => store.GetExactAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(store => store.UpsertAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<SmtpTransportProfile>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Organization_email_update_and_reset_are_denied_in_effective_read_only_context()
    {
        var profiles = new Mock<IEmailNotificationProfileStore>();
        var organizationAccess = ScopedOrganizationAccess();
        var service = CreateService(
            ["OrganizationConfiguration.View", "OrganizationConfiguration.Manage"],
            profiles.Object, organizationAccess.Object, out _, ReadOnlyManagementContext());
        var updateRequest = new UpdateEmailNotificationProfileRequest(
            Enabled: true,
            Host: "smtp.example.test",
            Port: 587,
            SecurityMode: SmtpSecurityMode.StartTlsRequired,
            AuthenticationMode: SmtpAuthenticationMode.UsernamePassword,
            UserName: "smtp-user",
            Password: "test-only-secret",
            ClearPassword: false,
            FromAddress: "notifications@example.test",
            FromName: "FOPX Notifications",
            ReplyToAddress: null,
            ExpectedVersion: null);

        var update = await Assert.ThrowsAsync<EmailNotificationSettingsException>(() => service.UpdateSettingsAsync(
            EmailNotificationProfileScope.Organization, OrganizationId, updateRequest));
        var reset = await Assert.ThrowsAsync<EmailNotificationSettingsException>(() => service.ResetSettingsAsync(
            EmailNotificationProfileScope.Organization, OrganizationId, expectedVersion: null));

        Assert.Equal(403, update.StatusCode);
        Assert.Equal("readOnlyManagementContext", update.Code);
        Assert.Equal(403, reset.StatusCode);
        Assert.Equal("readOnlyManagementContext", reset.Code);
        profiles.Verify(store => store.GetExactAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(store => store.UpsertAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<SmtpTransportProfile>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(store => store.DeleteAsync(
            It.IsAny<EmailNotificationProfileScope>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        organizationAccess.Verify(access => access.HasScopedOrganizationAdminAuthorityAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IEmailNotificationOrganizationScopeAccess> ScopedOrganizationAccess()
    {
        var access = new Mock<IEmailNotificationOrganizationScopeAccess>();
        access.Setup(service => service.HasScopedOrganizationAdminAuthorityAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken _) => id == OrganizationId);
        access.Setup(service => service.HasAccessAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken _) => id == OrganizationId);
        return access;
    }

    private static IManagementContextAccessor ReadOnlyManagementContext()
    {
        var accessor = new Mock<IManagementContextAccessor>();
        accessor.SetupGet(item => item.Current).Returns(new ManagementContext(
            ActorId, TenantId, ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs, null, null, null, ManagementScope.Empty));
        return accessor.Object;
    }

    private static EmailNotificationSettingsService CreateService(
        IReadOnlyCollection<string> permissions,
        IEmailNotificationProfileStore profiles,
        IEmailNotificationOrganizationScopeAccess organizationAccess,
        out Mock<IEmailNotificationTestOperations> testOperations,
        IManagementContextAccessor? managementContextAccessor = null)
    {
        var allowedPolicies = permissions.ToHashSet(StringComparer.Ordinal);
        var authorization = new Mock<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) => allowedPolicies.Contains(policy)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        var destinationPolicy = new Mock<ISmtpDestinationPolicy>();
        destinationPolicy.Setup(policy => policy.IsTrustedRelayHostAuthorized(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>()))
            .Returns(false);
        testOperations = new Mock<IEmailNotificationTestOperations>();
        var users = new Mock<IUserRepository>();
        var audit = new Mock<IAuditService>();
        var httpContext = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D")),
                    new Claim("tenantId", TenantId.ToString("D"))
                ], "OrganizationAdminEmailSettingsTests"))
            }
        };
        return new EmailNotificationSettingsService(
            profiles,
            organizationAccess,
            destinationPolicy.Object,
            testOperations.Object,
            users.Object,
            authorization.Object,
            httpContext,
            audit.Object,
            managementContextAccessor);
    }
}
