using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class PaymentRequestWorkflowPolicyTests
{
    [Fact]
    public void ParallelSetup_AllowsNullPositionsAndOptionalApprovers_WhenMandatoryApproverExists()
    {
        var approvers = new[]
        {
            new PaymentRequestApproverSelectionDto(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory),
            new PaymentRequestApproverSelectionDto(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Optional)
        };

        var errors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            FinanceApprovalModes.Parallel,
            PaymentRequestApprovalModes.Parallel,
            approvers);

        Assert.Empty(errors);
    }

    [Fact]
    public void ApprovalSetup_RejectsTheRequesterAsAnApproverByDefault()
    {
        var requesterId = Guid.NewGuid();
        var approvers = new[]
        {
            new PaymentRequestApproverSelectionDto(requesterId, null, PaymentRequestApproverRequirements.Mandatory)
        };

        var errors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            FinanceApprovalModes.Parallel,
            PaymentRequestApprovalModes.Parallel,
            approvers,
            requesterId);

        Assert.Contains("requester_cannot_approve", errors);
        Assert.False(PaymentRequestWorkflowPolicy.IsRequesterEligibleForApproval(requesterId, requesterId));
        Assert.True(PaymentRequestWorkflowPolicy.IsRequesterEligibleForApproval(requesterId, Guid.NewGuid()));
    }

    [Fact]
    public void ParallelSetup_RequiresAtLeastOneMandatoryApprover()
    {
        var approvers = new[]
        {
            new PaymentRequestApproverSelectionDto(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Optional)
        };

        var errors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            FinanceApprovalModes.Parallel,
            PaymentRequestApprovalModes.Parallel,
            approvers);

        Assert.Contains("parallel_requires_mandatory_approver", errors);
    }

    [Fact]
    public void SequentialRejection_StopsAndDoesNotActivateFollowingApprovers()
    {
        var firstApprover = Guid.NewGuid();
        var nextApprover = Guid.NewGuid();
        PaymentRequestApproverState[] states =
        [
            new(firstApprover, 1, PaymentRequestApproverRequirements.Mandatory, PaymentRequestDecisions.Rejected),
            new(nextApprover, 2, PaymentRequestApproverRequirements.Mandatory, null)
        ];

        Assert.Equal(PaymentRequestStatuses.Rejected, PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(states));
        Assert.Null(PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(states));
        Assert.False(PaymentRequestWorkflowPolicy.CanDecideSequentially(nextApprover, PaymentRequestStatuses.Pending, states));
    }

    [Fact]
    public void ParallelOptionalRejection_DoesNotBlockMandatoryApproval()
    {
        PaymentRequestApproverState[] states =
        [
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory, PaymentRequestDecisions.Approved),
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Optional, PaymentRequestDecisions.Rejected)
        ];

        Assert.Equal(PaymentRequestStatuses.Approved, PaymentRequestWorkflowPolicy.EvaluateParallelStatus(states));
    }

    [Fact]
    public void AvailableBudget_SubtractsOnlyTheApprovedRequestAllocation()
    {
        Assert.Equal(625m, ProjectFinancialBalanceCalculator.CalculateAvailableBudget(1000m, 375m));
        Assert.Null(ProjectFinancialBalanceCalculator.CalculateAvailableBudget(null, 375m));
    }
}
