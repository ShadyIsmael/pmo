namespace NEXUS.Modules.Documents.Application;

public static class DocumentCategoryPermissions
{
    public const string View = "DocumentCategory.View";
    public const string Create = "DocumentCategory.Create";
    public const string Edit = "DocumentCategory.Edit";
    public const string ActivateDeactivate = "DocumentCategory.ActivateDeactivate";

    // Policy name for the narrow category lookup needed by an authorized
    // template registration operation. This is not a catalog permission.
    public const string TemplateOptionsLookupPolicy = "DocumentCategory.TemplateOptionsLookup";
}
