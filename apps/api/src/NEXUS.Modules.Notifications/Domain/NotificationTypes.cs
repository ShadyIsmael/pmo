namespace NEXUS.Modules.Notifications.Domain;

public static class NotificationTypes
{
    public const string ProjectHandover = "ProjectHandover";
    public const string ProgramHandover = "ProgramHandover";
    public const string PortfolioHandover = "PortfolioHandover";
    public const string ProjectChangeRequest = "ProjectChangeRequest";
    public const string PaymentDue = "PaymentDue";
    public const string PaymentOverdue = "PaymentOverdue";
    public const string ProjectAnnualBudgetApproval = "ProjectAnnualBudgetApproval";
    public const string MeetingReminder = "MeetingReminder";
    public const string MeetingInvitation = "MeetingInvitation";
    public const string MeetingStarted = "MeetingStarted";
    public const string MeetingMinutesPublished = "MeetingMinutesPublished";
    public const string MeetingMinutesComment = "MeetingMinutesComment";
    public const string MeetingMinutesCommentReply = "MeetingMinutesCommentReply";
    public const string MeetingMinutesShared = "MeetingMinutesShared";
    public const string MeetingCompleted = "MeetingCompleted";
    public const string RiskActionFollowUpDue = "RiskActionFollowUpDue";

    public static IReadOnlyList<string> Supported { get; } =
        [ProjectHandover, ProgramHandover, PortfolioHandover, ProjectChangeRequest, PaymentDue, PaymentOverdue, ProjectAnnualBudgetApproval, MeetingReminder, MeetingInvitation, MeetingStarted, MeetingMinutesPublished, MeetingMinutesComment, MeetingMinutesCommentReply, MeetingMinutesShared, MeetingCompleted, RiskActionFollowUpDue];
}
