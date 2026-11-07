using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Pure transition rules for Payment Request approvals. Persistence, authorization,
/// project locking, and audit writes belong to the project-scoped command service.
/// </summary>
public static class PaymentRequestWorkflowPolicy
{
    public static IReadOnlyList<string> ValidateDraft(
        CreatePaymentRequestDto request,
        string? projectCurrency)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("request_title_required");

        if (request.Amount <= 0)
            errors.Add("request_amount_must_be_positive");

        if (string.IsNullOrWhiteSpace(request.Reason))
            errors.Add("request_reason_required");

        if (string.IsNullOrWhiteSpace(projectCurrency)
            || projectCurrency.Trim().Length != 3
            || projectCurrency.Trim().Any(character => !char.IsAsciiLetter(character)))
            errors.Add("project_currency_unavailable");

        return errors;
    }

    public static IReadOnlyList<string> ValidateApprovalSetup(
        string? projectApprovalMode,
        string? requestApprovalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto>? approvers)
    {
        var errors = new List<string>();
        var selectedApprovers = approvers?.ToArray() ?? [];

        if (projectApprovalMode is null)
            errors.Add("project_approval_mode_unconfigured");
        else if (!FinanceApprovalModes.IsValid(projectApprovalMode))
            errors.Add("project_approval_mode_invalid");

        if (!PaymentRequestApprovalModes.IsValid(requestApprovalMode))
            errors.Add("request_approval_mode_invalid");
        else if (projectApprovalMode is not null
            && !string.Equals(projectApprovalMode, FinanceApprovalModes.Both, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(projectApprovalMode, requestApprovalMode, StringComparison.OrdinalIgnoreCase))
            errors.Add("request_approval_mode_not_allowed_by_project");

        if (selectedApprovers.Length == 0)
            errors.Add("approver_required");

        if (selectedApprovers.Any(item => item.UserId == Guid.Empty))
            errors.Add("approver_user_required");

        if (selectedApprovers.Select(item => item.UserId).Distinct().Count() != selectedApprovers.Length)
            errors.Add("approver_users_must_be_unique");

        var isSequential = string.Equals(requestApprovalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase);
        if (isSequential && selectedApprovers.Any(item => item.Position is null or < 1))
            errors.Add("sequential_approver_position_required");

        if (!isSequential && selectedApprovers.Any(item => item.Position is not null))
            errors.Add("parallel_approver_position_not_allowed");

        var orderedPositions = selectedApprovers.Where(item => item.Position.HasValue).Select(item => item.Position!.Value).ToArray();
        if (orderedPositions.Distinct().Count() != orderedPositions.Length)
            errors.Add("approver_positions_must_be_unique");

        if (selectedApprovers.Any(item => !PaymentRequestApproverRequirements.IsValid(item.Requirement)))
            errors.Add("approver_requirement_invalid");

        if (isSequential
            && selectedApprovers.Any(item => !string.Equals(item.Requirement, PaymentRequestApproverRequirements.Mandatory, StringComparison.OrdinalIgnoreCase)))
            errors.Add("sequential_approvers_must_be_mandatory");

        if (string.Equals(requestApprovalMode, PaymentRequestApprovalModes.Parallel, StringComparison.OrdinalIgnoreCase)
            && !selectedApprovers.Any(item => string.Equals(item.Requirement, PaymentRequestApproverRequirements.Mandatory, StringComparison.OrdinalIgnoreCase)))
            errors.Add("parallel_requires_mandatory_approver");

        return errors;
    }

    public static Guid? GetActiveSequentialApprover(IReadOnlyCollection<PaymentRequestApproverState> approvers)
    {
        if (approvers.Count == 0 || approvers.Any(item => Is(item.Decision, PaymentRequestDecisions.Rejected)))
            return null;

        foreach (var approver in approvers.OrderBy(item => item.Position))
        {
            if (approver.Decision is null) return approver.UserId;
            if (!Is(approver.Decision, PaymentRequestDecisions.Approved)) return null;
        }

        return null;
    }

    public static string EvaluateSequentialStatus(IReadOnlyCollection<PaymentRequestApproverState> approvers)
    {
        if (approvers.Count == 0)
            throw new ArgumentException("A sequential approval needs at least one approver.", nameof(approvers));

        if (approvers.Any(item => Is(item.Decision, PaymentRequestDecisions.Rejected)))
            return PaymentRequestStatuses.Rejected;

        return approvers.All(item => Is(item.Decision, PaymentRequestDecisions.Approved))
            ? PaymentRequestStatuses.Approved
            : PaymentRequestStatuses.Pending;
    }

    public static string EvaluateParallelStatus(IReadOnlyCollection<PaymentRequestApproverState> approvers)
    {
        var mandatory = approvers
            .Where(item => Is(item.Requirement, PaymentRequestApproverRequirements.Mandatory))
            .ToArray();

        if (mandatory.Length == 0)
            throw new ArgumentException("Parallel approval needs at least one mandatory approver.", nameof(approvers));

        if (mandatory.Any(item => Is(item.Decision, PaymentRequestDecisions.Rejected)))
            return PaymentRequestStatuses.Rejected;

        return mandatory.All(item => Is(item.Decision, PaymentRequestDecisions.Approved))
            ? PaymentRequestStatuses.Approved
            : PaymentRequestStatuses.Pending;
    }

    public static bool CanDecideSequentially(
        Guid approverUserId,
        string requestStatus,
        IReadOnlyCollection<PaymentRequestApproverState> approvers) =>
        Is(requestStatus, PaymentRequestStatuses.Pending)
        &&
        GetActiveSequentialApprover(approvers) == approverUserId;

    public static bool CanDecideInParallel(
        Guid approverUserId,
        string requestStatus,
        IReadOnlyCollection<PaymentRequestApproverState> approvers) =>
        Is(requestStatus, PaymentRequestStatuses.Pending)
        && approvers.Any(item => item.UserId == approverUserId && item.Decision is null);

    public static bool CanCancelBeforeApproval(string requestStatus) =>
        Is(requestStatus, PaymentRequestStatuses.Draft)
        || Is(requestStatus, PaymentRequestStatuses.Pending);

    /// <summary>
    /// True when the request currently consumes approved/allocation budget in persisted status terms.
    /// Availability checks must also include submitted reservations (Pending) where over-submission must be prevented.
    /// </summary>
    public static bool ReservesBudget(string requestStatus) =>
        Is(requestStatus, PaymentRequestStatuses.Approved)
        || Is(requestStatus, PaymentRequestStatuses.Pending);

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
