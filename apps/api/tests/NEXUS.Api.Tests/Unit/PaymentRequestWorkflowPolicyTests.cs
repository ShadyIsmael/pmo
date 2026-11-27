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
    public void SequentialReturnForRevision_StopsFollowingApprovers()
    {
        var firstApprover = Guid.NewGuid();
        var nextApprover = Guid.NewGuid();
        PaymentRequestApproverState[] states =
        [
            new(firstApprover, 1, PaymentRequestApproverRequirements.Mandatory, PaymentRequestDecisions.ReturnedForRevision),
            new(nextApprover, 2, PaymentRequestApproverRequirements.Mandatory, null)
        ];

        Assert.Equal(PaymentRequestStatuses.ReturnedForRevision, PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(states));
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
    public void ParallelOptionalReturn_DoesNotBlockMandatoryApproval()
    {
        PaymentRequestApproverState[] states =
        [
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory, PaymentRequestDecisions.Approved),
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Optional, PaymentRequestDecisions.ReturnedForRevision)
        ];

        Assert.Equal(PaymentRequestStatuses.Approved, PaymentRequestWorkflowPolicy.EvaluateParallelStatus(states));
    }

    [Fact]
    public void ParallelMandatoryReturn_ReleasesActiveAnnualAllocation()
    {
        PaymentRequestApproverState[] states =
        [
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory, PaymentRequestDecisions.ReturnedForRevision),
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory, null),
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Optional, null)
        ];

        var status = PaymentRequestWorkflowPolicy.EvaluateParallelStatus(states);
        Assert.Equal(PaymentRequestStatuses.ReturnedForRevision, status);
        Assert.False(PaymentRequestWorkflowPolicy.ReservesBudget(status));
        Assert.True(PaymentRequestWorkflowPolicy.CanCancelBeforeApproval(status));
    }

    [Theory]
    [InlineData(PaymentRequestStatuses.Pending, true)]
    [InlineData(PaymentRequestStatuses.Approved, true)]
    [InlineData(PaymentRequestStatuses.ReturnedForRevision, false)]
    [InlineData(PaymentRequestStatuses.Rejected, false)]
    [InlineData(PaymentRequestStatuses.Cancelled, false)]
    public void BudgetReservation_IsDerivedFromActiveRequestStatus(string status, bool expected)
    {
        Assert.Equal(expected, PaymentRequestWorkflowPolicy.ReservesBudget(status));
    }

    [Fact]
    public void ContainsRequester_DetectsExactRequesterOnly()
    {
        var requesterId = Guid.NewGuid();
        PaymentRequestApproverSelectionDto[] approvers =
        [
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory),
            new(requesterId, null, PaymentRequestApproverRequirements.Optional)
        ];

        Assert.True(PaymentRequestWorkflowPolicy.ContainsRequester(requesterId, approvers));
        Assert.False(PaymentRequestWorkflowPolicy.ContainsRequester(Guid.NewGuid(), approvers));
        Assert.False(PaymentRequestWorkflowPolicy.ContainsRequester(null, approvers));
        Assert.False(PaymentRequestWorkflowPolicy.ContainsRequester(requesterId, null));
    }

    [Fact]
    public void SequentialSetup_RejectsRequesterAssignment()
    {
        var requesterId = Guid.NewGuid();
        PaymentRequestApproverSelectionDto[] approvers =
        [
            new(Guid.NewGuid(), 1, PaymentRequestApproverRequirements.Mandatory),
            new(requesterId, 2, PaymentRequestApproverRequirements.Mandatory)
        ];

        var errors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            FinanceApprovalModes.Both,
            PaymentRequestApprovalModes.Sequential,
            approvers,
            requesterId);

        Assert.Contains(PaymentRequestWorkflowPolicy.RequesterCannotBeApprover, errors);
    }

    [Fact]
    public void ParallelSetup_RejectsRequesterAssignment()
    {
        var requesterId = Guid.NewGuid();
        PaymentRequestApproverSelectionDto[] approvers =
        [
            new(Guid.NewGuid(), null, PaymentRequestApproverRequirements.Mandatory),
            new(requesterId, null, PaymentRequestApproverRequirements.Optional)
        ];

        var errors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            FinanceApprovalModes.Both,
            PaymentRequestApprovalModes.Parallel,
            approvers,
            requesterId);

        Assert.Contains(PaymentRequestWorkflowPolicy.RequesterCannotBeApprover, errors);
    }

    [Fact]
    public void AvailableBudget_SubtractsOnlyTheApprovedRequestAllocation()
    {
        Assert.Equal(625m, ProjectFinancialBalanceCalculator.CalculateAvailableBudget(1000m, 375m));
        Assert.Null(ProjectFinancialBalanceCalculator.CalculateAvailableBudget(null, 375m));
    }
}
