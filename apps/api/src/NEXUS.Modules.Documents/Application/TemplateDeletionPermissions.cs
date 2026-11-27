namespace NEXUS.Modules.Documents.Application;

/// <summary>Canonical, independently grantable permissions for template deletion requests.</summary>
public static class TemplateDeletionPermissions
{
    public const string Request = "Document.TemplateDeletion.Request";
    public const string View = "Document.TemplateDeletion.View";
    public const string Approve = "Document.TemplateDeletion.Approve";
    public const string Reject = "Document.TemplateDeletion.Reject";
}
