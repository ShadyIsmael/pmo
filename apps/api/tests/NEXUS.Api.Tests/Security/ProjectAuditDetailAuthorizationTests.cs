using NEXUS.Modules.Audit.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectAuditDetailAuthorizationTests
{
    [Theory]
    [InlineData("Project", "Project.View", "ProjectChangeRequest")]
    [InlineData("ProjectSnapshot", "Project.View", "ProjectChangeRequest")]
    [InlineData("ProjectBaselineVersion", "Project.View", "ProjectChangeRequest")]
    [InlineData("ProjectChangeRequest", "ChangeRequest.View", "ProjectStatusReport")]
    [InlineData("ProjectStatusReport", "StatusReport.View", "ProjectChangeRequest")]
    [InlineData("ProjectReportingConfiguration", "StatusReport.View", "ProjectChangeRequest")]
    [InlineData("ProjectHandoverRequest", "Handover.View", "ProjectChangeRequest")]
    [InlineData("ProjectStakeholder", "Stakeholders.Read", "ProjectStatusReport")]
    [InlineData("ProjectPerson", "Project.View", "ProjectStakeholder")]
    [InlineData("ProjectMember", "Project.View", "ProjectStakeholder")]
    public void Audit_detail_requires_the_exact_permission_for_its_record_type(
        string entityType,
        string permittedKey,
        string otherEntityType)
    {
        var access = new AuditTrailAccess(
            IncludeFinancialDetail: false,
            DetailPermissionKeys: new HashSet<string>([permittedKey], StringComparer.Ordinal));

        Assert.True(access.CanViewSensitiveDetailsFor(entityType));
        Assert.False(access.CanViewSensitiveDetailsFor(otherEntityType));
    }

    [Fact]
    public void Project_edit_does_not_grant_governance_audit_detail()
    {
        var access = new AuditTrailAccess(
            IncludeFinancialDetail: false,
            DetailPermissionKeys: new HashSet<string>(["Project.Edit"], StringComparer.Ordinal));

        Assert.False(access.IncludeGovernanceDetail);
        Assert.False(access.CanViewSensitiveDetailsFor("Project"));
        Assert.False(access.CanViewSensitiveDetailsFor("ProjectSnapshot"));
        Assert.False(access.CanViewSensitiveDetailsFor("ProjectBaselineVersion"));
        Assert.False(access.CanViewSensitiveDetailsFor("ProjectChangeRequest"));
        Assert.False(access.CanViewSensitiveDetailsFor("ProjectStatusReport"));
        Assert.False(access.CanViewSensitiveDetailsFor("ProjectStakeholder"));
    }
}
