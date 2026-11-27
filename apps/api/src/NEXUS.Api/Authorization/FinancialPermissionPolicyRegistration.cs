using Microsoft.AspNetCore.Authorization;

namespace NEXUS.Api.Authorization;

public static class FinancialPermissionPolicyRegistration
{
    public static void AddCanonicalFinancialPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy("ProjectAnnualBudget.View", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.View"));
        options.AddPolicy("ProjectAnnualBudget.Create", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.Create"));
        options.AddPolicy("ProjectAnnualBudget.EditDraft", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.EditDraft"));
        options.AddPolicy("ProjectAnnualBudget.EditOrManageLines", policy =>
            policy.RequireAssertion(context => context.User.HasClaim("permission", "ProjectAnnualBudget.EditDraft")
                || context.User.HasClaim("permission", "ProjectAnnualBudget.ManageLines")));
        options.AddPolicy("ProjectAnnualBudget.ConfigureDraftApproval", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.ConfigureDraftApproval"));
        options.AddPolicy("ProjectAnnualBudget.SubmitForReview", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.SubmitForReview"));
        options.AddPolicy("ProjectAnnualBudget.Approve", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.Approve"));
        options.AddPolicy("ProjectAnnualBudget.ReviseApproved", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.ReviseApproved"));
        options.AddPolicy("ProjectAnnualBudget.ManageApprovalConfiguration", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.ManageApprovalConfiguration"));
        options.AddPolicy("ProjectAnnualBudget.Delete", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.Delete"));
        options.AddPolicy("ProjectAnnualBudget.Withdraw", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.Withdraw"));
        options.AddPolicy("ProjectAnnualBudgetClosing.View", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudgetClosing.View"));
        options.AddPolicy("ProjectAnnualBudget.Close", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.Close"));
        options.AddPolicy("ProjectAnnualBudget.CloseApprove", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.CloseApprove"));
        options.AddPolicy("ProjectAnnualBudget.CarryForward", policy =>
            policy.RequireClaim("permission", "ProjectAnnualBudget.CarryForward"));
        options.AddPolicy("ProjectPaymentRequest.Approve", policy =>
            policy.RequireClaim("permission", "ProjectPaymentRequest.Approve"));
        options.AddPolicy("PaymentSettlement.Record", policy =>
            policy.RequireClaim("permission", "PaymentSettlement.Record"));
        options.AddPolicy("CostUtilization.Export", policy =>
        {
            policy.RequireClaim("permission", "Resource.View");
            policy.RequireClaim("permission", "CostUtilization.Export");
        });
        options.AddPolicy("ProjectFinancialFoundation.View", policy =>
            policy.RequireClaim("permission", "ProjectFinancialFoundation.View"));
        options.AddPolicy("ProgramFinancialRollup.View", policy =>
            policy.RequireClaim("permission", "ProgramFinancialRollup.View"));
        options.AddPolicy("PortfolioFinancialRollup.View", policy =>
            policy.RequireClaim("permission", "PortfolioFinancialRollup.View"));
    }
}
