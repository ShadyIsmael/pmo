namespace NEXUS.Modules.Notifications.Domain;

public static class NotificationTypes
{
    public const string ProjectHandover = "ProjectHandover";
    public const string ProgramHandover = "ProgramHandover";
    public const string PortfolioHandover = "PortfolioHandover";
    public const string ProjectChangeRequest = "ProjectChangeRequest";
    public const string PaymentDue = "PaymentDue";
    public const string PaymentOverdue = "PaymentOverdue";

    public static IReadOnlyList<string> Supported { get; } =
    [ProjectHandover, ProgramHandover, PortfolioHandover, ProjectChangeRequest, PaymentDue, PaymentOverdue];
}
