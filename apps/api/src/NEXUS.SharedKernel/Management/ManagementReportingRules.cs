namespace NEXUS.SharedKernel.Management;

public static class ManagementReportingRules
{
    public const int StatusReportFreshnessDays = 30;

    public static string Freshness(DateTime reportDate, DateTime now)
        => reportDate >= now.AddDays(-StatusReportFreshnessDays) ? "Current" : "Stale";
}
