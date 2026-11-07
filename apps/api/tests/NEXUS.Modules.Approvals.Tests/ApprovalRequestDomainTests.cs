using Moq;
using NEXUS.Modules.Approvals.Application;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.Approvals.Tests;

public class ApprovalRequestDomainTests
{
    [Fact]
    public void Submit_FromDraft_SetsSubmitted()
    {
        var request = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Status = ApprovalRequest.StatusDraft, RequestedBy = Guid.NewGuid(), ApprovalLevel = 1 };
        request.Submit("user-1");
        Assert.Equal(ApprovalRequest.StatusSubmitted, request.Status);
        Assert.Equal("user-1", request.UpdatedBy);
    }

    [Fact]
    public void Submit_FromNonDraft_Throws()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusSubmitted };
        Assert.Throws<InvalidOperationException>(() => request.Submit("user-1"));
    }

    [Fact]
    public void Approve_FromSubmitted_ByAssignee_SetsApproved()
    {
        var assignee = Guid.NewGuid();
        var request = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Status = ApprovalRequest.StatusSubmitted, CurrentApproverId = assignee, ApprovalLevel = 1 };
        request.Approve(assignee, "Looks good", "user-1");
        Assert.Equal(ApprovalRequest.StatusApproved, request.Status);
        Assert.Single(request.Actions);
        Assert.Equal("Approve", request.Actions.First().Action);
        Assert.Null(request.CurrentApproverId);
    }

    [Fact]
    public void Approve_FromDraft_Throws()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusDraft, CurrentApproverId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid(), "ok", "user-1"));
    }

    [Fact]
    public void Approve_ByWrongUser_Throws()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusSubmitted, CurrentApproverId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid(), "ok", "user-1"));
    }

    [Fact]
    public void Reject_RequiresComment()
    {
        var assignee = Guid.NewGuid();
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusSubmitted, CurrentApproverId = assignee };
        Assert.Throws<InvalidOperationException>(() => request.Reject(assignee, null, "user-1"));
    }

    [Fact]
    public void Reject_WithComment_SetsRejected()
    {
        var assignee = Guid.NewGuid();
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusSubmitted, CurrentApproverId = assignee, ApprovalLevel = 2 };
        request.Reject(assignee, "Needs changes", "user-1");
        Assert.Equal(ApprovalRequest.StatusRejected, request.Status);
        Assert.Single(request.Actions);
        Assert.Equal("Reject", request.Actions.First().Action);
    }

    [Fact]
    public void Delegate_ChangesCurrentApprover()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusSubmitted, CurrentApproverId = from, ApprovalLevel = 1 };
        request.Delegate(to, "On leave", "user-1");
        Assert.Equal(to, request.CurrentApproverId);
        Assert.Single(request.Actions);
        Assert.Equal("Delegate", request.Actions.First().Action);
    }

    [Fact]
    public void Delegate_FromApproved_Throws()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusApproved };
        Assert.Throws<InvalidOperationException>(() => request.Delegate(Guid.NewGuid(), "reason", "user-1"));
    }

    [Fact]
    public void Cancel_FromDraft_SetsCancelled()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusDraft, RequestedBy = Guid.NewGuid() };
        request.Cancel("user-1");
        Assert.Equal(ApprovalRequest.StatusCancelled, request.Status);
    }

    [Fact]
    public void Cancel_FromApproved_Throws()
    {
        var request = new ApprovalRequest { Status = ApprovalRequest.StatusApproved };
        Assert.Throws<InvalidOperationException>(() => request.Cancel("user-1"));
    }
}
