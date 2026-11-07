using Microsoft.AspNetCore.Authorization;

namespace NEXUS.Api.Authorization;

public static class FinancialPermissionPolicyRegistration
{
    public static void AddCanonicalFinancialActionPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

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
