using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public enum PermissionLevel { View, Action }
public enum PermissionScope { Tenant, Organization, Portfolio, Program, Project, Platform, CrossTenant, SystemOwner }

public sealed record PermissionCatalogEntry(
    string SeedId, string Key, string Resource, string Action,
    string ModuleId, string ModuleNameEn, string ModuleNameAr,
    string ScreenId, string ScreenNameEn, string ScreenNameAr, PermissionLevel Level,
    string LabelEn, string LabelAr, string DescriptionEn, string DescriptionAr,
    string Sensitivity, bool IsSensitive, IReadOnlyList<string> RequiredPermissionKeys,
    bool IsActive, PermissionScope Scope = PermissionScope.Tenant)
{
    // Compatibility metadata only. Grantability is evaluated by
    // IPermissionProtectionPolicy plus the separate permission-scope rules.
    public bool IsAssignable => IsActive;

    // Compatibility metadata for existing Roles & Permissions clients: every
    // selectable custom-role category can receive this permission, subject to
    // ordinary delegation, scope, dependency, and the central protection policy.
    public IReadOnlyList<string> AllowedCategories => RoleCategories.Selectable;
}

public static class PermissionCatalog
{
    private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog =
    [
        new(
            "b0000169-0000-0000-0000-000000000169", "Audit.View", "Audit", "View",
            "administration", "Administration", "الإدارة",
            "audit-trail", "Audit Trail", "سجل التدقيق", PermissionLevel.View,
            "View", "عرض", "Read the tenant audit trail and governance activity log", "تنفيذ إجراء عرض سجل التدقيق على Audit trail مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000107-0000-0000-0000-000000000107", "ChangeRequest.Approve", "ChangeRequest", "Approve",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "change-requests", "Change Requests", "طلبات التغيير", PermissionLevel.Action,
            "Approve or reject", "اعتماد أو رفض", "Approve or reject project change requests", "تنفيذ إجراء طلب التغيير · اعتماد أو رفض على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, [], true),
        new(
            "b0000106-0000-0000-0000-000000000106", "ChangeRequest.Manage", "ChangeRequest", "Manage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "change-requests", "Change Requests", "طلبات التغيير", PermissionLevel.Action,
            "Manage requests", "إدارة الطلبات", "Create, update, and cancel project change requests", "تنفيذ إجراء إنشاء طلبات التغيير وتعديلها وإلغاؤها على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["ChangeRequest.View"], true),
        new(
            "b0000105-0000-0000-0000-000000000105", "ChangeRequest.View", "ChangeRequest", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "change-requests", "Change Requests", "طلبات التغيير", PermissionLevel.View,
            "View", "عرض", "View project change requests", "تنفيذ إجراء طلبات التغيير · عرض على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000154-0000-0000-0000-000000000154", "Directory.ExecuteSync", "Directory", "ExecuteSync",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Run sync", "تشغيل المزامنة", "Execute selected directory synchronization actions", "تنفيذ إجراء تنفيذ مزامنة الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Directory.View"], true),
        new(
            "b0000155-0000-0000-0000-000000000155", "Directory.ManageMappings", "Directory", "ManageMappings",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Manage mappings", "إدارة ربط الحقول", "Manage directory field, group, and organization mappings", "تنفيذ إجراء إدارة تعيينات الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000156-0000-0000-0000-000000000156", "Directory.ManageProvisioningPolicy", "Directory", "ManageProvisioningPolicy",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Manage provisioning", "إدارة سياسة توفير الحسابات", "Manage directory provisioning policy", "تنفيذ إجراء إدارة سياسة إنشاء الحسابات على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Directory.View"], true),
        new(
            "b0000153-0000-0000-0000-000000000153", "Directory.PreviewSync", "Directory", "PreviewSync",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Preview sync", "معاينة المزامنة", "Preview directory synchronization", "تنفيذ إجراء معاينة مزامنة الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000152-0000-0000-0000-000000000152", "Directory.TestConnection", "Directory", "TestConnection",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Test connection", "اختبار الاتصال", "Test directory connectivity", "تنفيذ إجراء اختبار اتصال الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000150-0000-0000-0000-000000000150", "Directory.View", "Directory", "View",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.View,
            "View", "عرض", "View directory integration configuration and sync history", "تنفيذ إجراء تكامل الدليل · عرض على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000137-0000-0000-0000-000000000137", "Document.Archive", "Document", "Archive",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Archive", "أرشفة", "Archive documents", "تنفيذ إجراء المستندات · أرشفة على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", true, ["Document.View"], true),
        new(
            "b0000135-0000-0000-0000-000000000135", "Document.Create", "Document", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Create", "إنشاء", "Upload documents", "تنفيذ إجراء المستندات · إنشاء على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000136-0000-0000-0000-000000000136", "Document.Edit", "Document", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Edit", "تعديل", "Upload document versions", "تنفيذ إجراء المستندات · تعديل على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000138-0000-0000-0000-000000000138", "Document.TemplateManage", "Document", "TemplateManage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Manage templates", "إدارة القوالب", "Manage reusable document templates", "تنفيذ إجراء إدارة قوالب المستندات على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000134-0000-0000-0000-000000000134", "Document.View", "Document", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "documents", "Documents", "المستندات", PermissionLevel.View,
            "View", "عرض", "View documents and templates", "تنفيذ إجراء المستندات · عرض على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000274-0000-0000-0000-000000000274", "DocumentCategory.View", "DocumentCategory", "View",
            "governance", "Governance", "الحوكمة",
            "document-center-categories", "Document Categories", "فئات المستندات", PermissionLevel.View,
            "View", "عرض", "View managed document categories for authorized organizations", "عرض فئات المستندات المدارة للمؤسسات المصرح بها.",
            "Low", false, [], true),
        new(
            "b0000275-0000-0000-0000-000000000275", "DocumentCategory.Create", "DocumentCategory", "Create",
            "governance", "Governance", "الحوكمة",
            "document-center-categories", "Document Categories", "فئات المستندات", PermissionLevel.Action,
            "Create", "إنشاء", "Create document categories for an authorized organization", "إنشاء فئات مستندات لمؤسسة مصرح بها.",
            "Low", false, ["DocumentCategory.View"], true),
        new(
            "b0000276-0000-0000-0000-000000000276", "DocumentCategory.Edit", "DocumentCategory", "Edit",
            "governance", "Governance", "الحوكمة",
            "document-center-categories", "Document Categories", "فئات المستندات", PermissionLevel.Action,
            "Edit", "تعديل", "Edit document category names and descriptions without breaking historical references", "تعديل أسماء فئات المستندات وأوصافها دون الإضرار بالمراجع التاريخية.",
            "Low", false, ["DocumentCategory.View"], true),
        new(
            "b0000277-0000-0000-0000-000000000277", "DocumentCategory.ActivateDeactivate", "DocumentCategory", "ActivateDeactivate",
            "governance", "Governance", "الحوكمة",
            "document-center-categories", "Document Categories", "فئات المستندات", PermissionLevel.Action,
            "Activate / deactivate", "تفعيل / إلغاء التفعيل", "Activate or deactivate document categories without deleting historical references", "تفعيل فئات المستندات أو إلغاء تفعيلها دون حذف المراجع التاريخية.",
            "Low", false, ["DocumentCategory.View"], true),
        new(
            "b0000228-0000-0000-0000-000000000228", "ProjectFinance.View", "ProjectFinance", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "project-finance", "Project Finance", "الشؤون المالية للمشروع", PermissionLevel.View,
            "Open Finance", "فتح الشؤون المالية", "Open the Project Finance navigation shell. Each finance workspace has its own data View permission and remains separately scoped.", "فتح هيكل التنقل لمساحة مالية المشروع. لكل مساحة مالية صلاحية عرض بيانات مستقلة وتظل خاضعة لنطاقها الخاص.",
            "Low", false, [], true),
        new(
            "b0000244-0000-0000-0000-000000000244", "Budget.View", "Budget", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.View,
            "View", "عرض", "View legacy budget records within authorized tenant and project or portfolio management scope.", "عرض سجلات الميزانية ضمن نطاق المستأجر والمشروع أو المحفظة المصرح به.",
            "High", true, [], true),
        new(
            "b0000245-0000-0000-0000-000000000245", "Budget.Create", "Budget", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Create", "إنشاء", "Create legacy budget records within authorized project or portfolio scope.", "إنشاء سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["Budget.View"], true),
        new(
            "b0000246-0000-0000-0000-000000000246", "Budget.Edit", "Budget", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Edit", "تعديل", "Edit legacy budget records within authorized project or portfolio scope.", "تعديل سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["Budget.View"], true),
        new(
            "b0000247-0000-0000-0000-000000000247", "Budget.Delete", "Budget", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Delete", "حذف", "Delete legacy budget records within authorized project or portfolio scope.", "حذف سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "Critical", true, ["Budget.View"], true),
        new(
            "b0000248-0000-0000-0000-000000000248", "BudgetRevision.Create", "BudgetRevision", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Create", "إنشاء", "Create a budget revision draft for a budget in the actor's authorized project or portfolio scope.", "إنشاء مسودة مراجعة ميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000249-0000-0000-0000-000000000249", "BudgetRevision.EditDraft", "BudgetRevision", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Edit draft", "تعديل المسودة", "Edit a draft budget revision within the actor's authorized project or portfolio scope.", "تعديل مسودة مراجعة ميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000250-0000-0000-0000-000000000250", "BudgetRevision.SubmitForReview", "BudgetRevision", "SubmitForReview",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Submit for review", "إرسال للمراجعة", "Submit a draft budget revision into the review workflow within authorized scope.", "إرسال مسودة مراجعة الميزانية إلى مسار المراجعة ضمن النطاق المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000251-0000-0000-0000-000000000251", "BudgetRevision.Cancel", "BudgetRevision", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Cancel", "إلغاء", "Cancel an eligible budget revision under workflow-state rules and authorized project scope.", "إلغاء مراجعة ميزانية مؤهلة وفق حالة سير العمل ونطاق المشروع المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000252-0000-0000-0000-000000000252", "BudgetRevision.Delete", "BudgetRevision", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Delete", "حذف", "Delete an eligible draft budget revision within authorized project scope.", "حذف مسودة مراجعة ميزانية مؤهلة ضمن نطاق المشروع المصرح به.",
            "Critical", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000253-0000-0000-0000-000000000253", "Cost.View", "Cost", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "costs", "Costs", "التكاليف", PermissionLevel.View,
            "View", "عرض", "View cost entries within authorized project management scope.", "عرض قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000254-0000-0000-0000-000000000254", "Cost.Create", "Cost", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Create", "إنشاء", "Create cost entries within authorized project management scope.", "إنشاء قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000255-0000-0000-0000-000000000255", "Cost.Edit", "Cost", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Edit", "تعديل", "Edit cost entries within authorized project management scope.", "تعديل قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000256-0000-0000-0000-000000000256", "Cost.Delete", "Cost", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Delete", "حذف", "Delete cost entries within authorized project management scope.", "حذف قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "Critical", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000269-0000-0000-0000-000000000269", "CostUtilization.Export", "CostUtilization", "Export",
            "capacity", "Resources", "الموارد",
            "cost-utilization", "Cost & Utilization", "التكلفة والاستغلال", PermissionLevel.Action,
            "Export", "تصدير", "Export Cost & Utilization data within the current Tenant and the user's authorized management scope. The export endpoint also requires Resource.View; resource cost-rate fields remain redacted unless Resource.ViewCostRate is effective.", "تصدير بيانات التكلفة والاستغلال ضمن المستأجر الحالي ونطاق الإدارة المصرح به للمستخدم. يتطلب مسار التصدير أيضًا صلاحية Resource.View، وتظل حقول معدلات تكلفة الموارد مخفية ما لم تكن صلاحية Resource.ViewCostRate فعالة.",
            "High", true, ["Resource.View"], true, PermissionScope.Tenant),
        new(
            "b0000257-0000-0000-0000-000000000257", "Commitment.View", "Commitment", "View",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.View,
            "View", "عرض", "View purchase commitments within authorized project management scope.", "عرض الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000258-0000-0000-0000-000000000258", "Commitment.Create", "Commitment", "Create",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.Action,
            "Create", "إنشاء", "Create purchase commitments within authorized project management scope.", "إنشاء الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000259-0000-0000-0000-000000000259", "Commitment.Edit", "Commitment", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.Action,
            "Edit", "تعديل", "Edit eligible purchase commitments within authorized project management scope.", "تعديل الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000260-0000-0000-0000-000000000260", "Commitment.Close", "Commitment", "Close",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.Action,
            "Close", "إقفال", "Close purchase commitments within authorized project management scope.", "إغلاق الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000261-0000-0000-0000-000000000261", "Commitment.Cancel", "Commitment", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.Action,
            "Cancel", "إلغاء", "Cancel eligible purchase commitments within authorized project management scope.", "إلغاء الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000262-0000-0000-0000-000000000262", "Commitment.Delete", "Commitment", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Commitments", "الالتزامات", PermissionLevel.Action,
            "Delete", "حذف", "Delete eligible purchase commitments within authorized project management scope.", "حذف الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "Critical", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000263-0000-0000-0000-000000000263", "PaymentObligation.View", "PaymentObligation", "View",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned Payments", "المدفوعات المخططة", PermissionLevel.View,
            "View", "عرض", "View payment obligations and their settlements within authorized project scope.", "عرض التزامات الدفع والتسويات ضمن نطاق المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000264-0000-0000-0000-000000000264", "PaymentObligation.Create", "PaymentObligation", "Create",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned Payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Create", "إنشاء", "Create a planned payment obligation within authorized project scope.", "إنشاء التزام دفع مخطط ضمن نطاق المشروع المصرح به.",
            "High", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000265-0000-0000-0000-000000000265", "PaymentObligation.Edit", "PaymentObligation", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned Payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Edit", "تعديل", "Edit an eligible payment obligation within authorized project scope.", "تعديل التزام دفع مؤهل ضمن نطاق المشروع المصرح به.",
            "High", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000266-0000-0000-0000-000000000266", "PaymentObligation.Delete", "PaymentObligation", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned Payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Delete", "حذف", "Delete an eligible payment obligation without settlements within authorized project scope.", "حذف التزام دفع مؤهل لا توجد عليه تسويات ضمن نطاق المشروع المصرح به.",
            "Critical", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000267-0000-0000-0000-000000000267", "PaymentObligationSettlement.Record", "PaymentObligationSettlement", "Record",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned Payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Record", "تسجيل", "Record an actual settlement against a planned payment obligation within authorized tenant, project, and fiscal-year scope.", "تسجيل تسوية فعلية مقابل التزام دفع مخطط ضمن نطاق المستأجر والمشروع والسنة المالية المصرح به.",
            "Critical", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000215-0000-0000-0000-000000000215", "BudgetRevision.View", "BudgetRevision", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.View,
            "View", "عرض", "View budget revisions within authorized Tenant and Project management scope.", "عرض مراجعات الميزانية ضمن نطاق المستأجر والمشروع المصرح به.",
            "Low", false, [], true, PermissionScope.Project),
        new(
            "b0000216-0000-0000-0000-000000000216", "BudgetRevision.Approve", "BudgetRevision", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget Revisions", "تعديلات الميزانية", PermissionLevel.Action,
            "Approve or reject", "اعتماد أو رفض", "Approve or reject a submitted budget revision. The requester cannot decide their own revision.", "اعتماد أو رفض مراجعة ميزانية مقدمة. لا يجوز لمقدم الطلب البت في طلبه.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000167-0000-0000-0000-000000000167", "Governance.AssignmentRecovery.Initiate", "Governance.AssignmentRecovery", "Initiate",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment Recovery", "استعادة الإسناد", PermissionLevel.Action,
            "Initiate", "بدء", "Initiate initial manager assignments through the existing approval workflow", "تنفيذ إجراء استعادة تعيينات الحوكمة · بدء على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Governance.AssignmentRecovery.View"], true),
        new(
            "b0000168-0000-0000-0000-000000000168", "Governance.AssignmentRecovery.Manage", "Governance.AssignmentRecovery", "Manage",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment Recovery", "استعادة الإسناد", PermissionLevel.Action,
            "Manage recovery", "إدارة الاستعادة", "Reconcile governance references, assign project sponsors, and initiate administrative handovers", "تنفيذ إجراء تنفيذ استعادة الإسناد على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Governance.AssignmentRecovery.View"], true),
        new(
            "b0000166-0000-0000-0000-000000000166", "Governance.AssignmentRecovery.View", "Governance.AssignmentRecovery", "View",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment Recovery", "استعادة الإسناد", PermissionLevel.View,
            "View", "عرض", "Discover tenant entities with missing governance assignments", "تنفيذ إجراء استعادة تعيينات الحوكمة · عرض على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000149-0000-0000-0000-000000000149", "Handover.AdminInitiate", "Handover", "AdminInitiate",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "handovers", "Handovers", "عمليات التسليم", PermissionLevel.Action,
            "Initiate handover", "بدء عملية التسليم", "Initiate administrative handovers and initial manager assignments", "تنفيذ إجراء التسليمات · بدء التسليمات إدارياً على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Handover.View"], true),
        new(
            "b0000110-0000-0000-0000-000000000110", "Handover.Approve", "Handover", "Approve",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "handovers", "Handovers", "عمليات التسليم", PermissionLevel.Action,
            "Approve or reject", "قبول أو رفض", "Accept or reject project and manager handovers", "تنفيذ إجراء تسليم المسؤولية · قبول أو رفض على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Handover.View"], true),
        new(
            "b0000109-0000-0000-0000-000000000109", "Handover.Manage", "Handover", "Manage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "handovers", "Handovers", "عمليات التسليم", PermissionLevel.Action,
            "Manage handovers", "إدارة عمليات التسليم", "Request or cancel project and manager handovers", "تنفيذ إجراء التسليمات · إدارة على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Handover.View"], true),
        new(
            "b0000108-0000-0000-0000-000000000108", "Handover.View", "Handover", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "handovers", "Handovers", "عمليات التسليم", PermissionLevel.View,
            "View", "عرض", "View project and manager handovers", "تنفيذ إجراء التسليمات · عرض على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000128-0000-0000-0000-000000000128", "Meeting.Create", "Meeting", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Create", "إنشاء", "Create meetings", "تنفيذ إجراء الاجتماعات · إنشاء على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000131-0000-0000-0000-000000000131", "Meeting.Delete", "Meeting", "Delete",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Delete", "حذف", "Delete meetings", "تنفيذ إجراء الاجتماعات · حذف على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Meeting.View"], true),
        new(
            "b0000129-0000-0000-0000-000000000129", "Meeting.Edit", "Meeting", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Edit", "تعديل", "Edit meetings and attendees", "تنفيذ إجراء الاجتماعات · تعديل على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000130-0000-0000-0000-000000000130", "Meeting.Manage", "Meeting", "Manage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Manage minutes", "إدارة محاضر الاجتماعات", "Manage minutes, comments, and work item links", "تنفيذ إجراء إدارة محاضر الاجتماعات والتعليقات وروابط عناصر العمل على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000127-0000-0000-0000-000000000127", "Meeting.View", "Meeting", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.View,
            "View", "عرض", "View meetings and meeting minutes", "تنفيذ إجراء الاجتماعات · عرض على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000171-0000-0000-0000-000000000171", "PaymentSettlement.Record", "PaymentSettlement", "Record",
            "financial-management", "Financial Management", "الإدارة المالية",
            "settlements-and-reversals", "Settlements & Reversals", "التسويات وعمليات العكس", PermissionLevel.Action,
            "Record settlement", "تسجيل التسوية", "Record actual project payment settlements within the actor's authorized project scope", "تنفيذ إجراء تسجيل تسوية دفعة على Payment Requests مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000118-0000-0000-0000-000000000118", "Planning.Lifecycle", "Planning", "Lifecycle",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "planning", "Project Plan", "خطة المشروع", PermissionLevel.Action,
            "Manage lifecycle", "إدارة دورة الحياة", "Manage planning working and baseline lifecycle", "تنفيذ إجراء التخطيط · إدارة دورة الحياة على Planning مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Planning.View"], true),
        new(
            "b0000116-0000-0000-0000-000000000116", "Planning.View", "Planning", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "planning", "Project Plan", "خطة المشروع", PermissionLevel.View,
            "View", "عرض", "View project planning data", "تنفيذ إجراء التخطيط · عرض على Planning مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000119-0000-0000-0000-000000000119", "Portfolio.View", "Portfolio", "View",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.View,
            "View", "عرض", "View portfolios", "عرض المحافظ الواقعة ضمن نطاق الإدارة المصرح للمستخدم.",
            "Low", false, [], true),
        new(
            "b0000121-0000-0000-0000-000000000121", "Program.View", "Program", "View",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.View,
            "View", "عرض", "View programs", "عرض البرامج الواقعة ضمن نطاق الإدارة المصرح للمستخدم.",
            "Low", false, [], true),
        new(
            "b0000102-0000-0000-0000-000000000102", "Project.Create", "Project", "Create",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Create", "إنشاء", "Create projects", "تنفيذ إجراء المشروعات · إنشاء على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Project.View"], true),
        new(
            "b0000104-0000-0000-0000-000000000104", "Project.Delete", "Project", "Delete",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Delete", "حذف", "Delete projects", "تنفيذ إجراء المشروعات · حذف على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Project.View"], true),
        new(
            "b0000103-0000-0000-0000-000000000103", "Project.Edit", "Project", "Edit",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Edit", "تعديل", "Edit project data", "تنفيذ إجراء المشروعات · تعديل على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Project.View"], true),
        new(
            "b0000101-0000-0000-0000-000000000101", "Project.View", "Project", "View",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.View,
            "View", "عرض", "View projects", "تنفيذ إجراء المشروعات · عرض على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000308-0000-0000-0000-000000000308", "ProjectLifecyclePhase.View", "ProjectLifecyclePhase", "View",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.View,
            "View phases", "عرض المراحل", "View the managed project phase catalog", "عرض دليل مراحل المشروع المُدار.",
            "Low", false, [], true),
        new(
            "b0000309-0000-0000-0000-000000000309", "ProjectLifecyclePhase.Create", "ProjectLifecyclePhase", "Create",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Create phase", "إنشاء مرحلة", "Create a project phase catalog entry", "إنشاء مرحلة في دليل مراحل المشروع.",
            "Low", false, ["ProjectLifecyclePhase.View"], true),
        new(
            "b0000310-0000-0000-0000-000000000310", "ProjectLifecyclePhase.Edit", "ProjectLifecyclePhase", "Edit",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Edit phase", "تعديل المرحلة", "Edit phase names, descriptions, defaults, ordering, and transition rules", "تعديل أسماء المراحل وأوصافها وقيمها الافتراضية وترتيبها وقواعد الانتقال.",
            "Low", false, ["ProjectLifecyclePhase.View"], true),
        new(
            "b0000311-0000-0000-0000-000000000311", "ProjectLifecyclePhase.ActivateDeactivate", "ProjectLifecyclePhase", "ActivateDeactivate",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Activate or deactivate phases", "تفعيل المراحل أو إلغاء تفعيلها", "Activate or deactivate phases without deleting historical project references", "تفعيل المراحل أو إلغاء تفعيلها دون حذف المراجع التاريخية للمشروعات.",
            "Low", false, ["ProjectLifecyclePhase.View"], true),
        new(
            "b0000312-0000-0000-0000-000000000312", "ProjectLifecycleStatus.View", "ProjectLifecycleStatus", "View",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.View,
            "View statuses", "عرض الحالات", "View the managed project status catalog", "عرض دليل حالات المشروع المُدار.",
            "Low", false, [], true),
        new(
            "b0000313-0000-0000-0000-000000000313", "ProjectLifecycleStatus.Create", "ProjectLifecycleStatus", "Create",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Create status", "إنشاء حالة", "Create a project status catalog entry", "إنشاء حالة في دليل حالات المشروع.",
            "Low", false, ["ProjectLifecycleStatus.View"], true),
        new(
            "b0000314-0000-0000-0000-000000000314", "ProjectLifecycleStatus.Edit", "ProjectLifecycleStatus", "Edit",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Edit status", "تعديل الحالة", "Edit status names, descriptions, defaults, ordering, and transition rules", "تعديل أسماء الحالات وأوصافها وقيمها الافتراضية وترتيبها وقواعد الانتقال.",
            "Low", false, ["ProjectLifecycleStatus.View"], true),
        new(
            "b0000315-0000-0000-0000-000000000315", "ProjectLifecycleStatus.ActivateDeactivate", "ProjectLifecycleStatus", "ActivateDeactivate",
            "administration", "Administration", "الإدارة",
            "project-lifecycle", "Project Lifecycle", "دورة حياة المشروع", PermissionLevel.Action,
            "Activate or deactivate statuses", "تفعيل الحالات أو إلغاء تفعيلها", "Activate or deactivate statuses without deleting historical project references", "تفعيل الحالات أو إلغاء تفعيلها دون حذف المراجع التاريخية للمشروعات.",
            "Low", false, ["ProjectLifecycleStatus.View"], true),
        new(
            "b0000316-0000-0000-0000-000000000316", "ProjectStatus.Change", "ProjectStatus", "Change",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Change project status", "تغيير حالة المشروع", "Apply a permitted project status transition and its configured reason and audit requirements", "تطبيق انتقال مسموح لحالة المشروع ومتطلبات السبب والتدقيق المحددة له.",
            "Medium", true, ["Project.View"], true, PermissionScope.Project),
        new(
            "b0000307-0000-0000-0000-000000000307", "ProjectPhase.Change", "ProjectPhase", "Change",
            "project-management", "Delivery Management", "إدارة التسليم",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Change project phase", "تغيير مرحلة المشروع", "Change a project's lifecycle phase after server-side phase gate validation.", "تغيير مرحلة دورة حياة المشروع بعد اجتياز التحقق من بوابة المرحلة على الخادم.",
            "Medium", true, ["Project.View"], true, PermissionScope.Project),
        new(
            "b0000170-0000-0000-0000-000000000170", "ProjectAnnualBudget.Approve", "ProjectAnnualBudget", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Review assigned budget", "مراجعة الميزانية المسندة", "Approve or reject an assigned Annual Budget review, or return it for changes; retain scope and separation-of-duties checks.", "اتخاذ قرار بشأن مراجعة ميزانية سنوية مسندة بالموافقة أو الرفض أو الإعادة للتعديل، مع تطبيق قيود النطاق وفصل المهام.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000176-0000-0000-0000-000000000176", "ProjectAnnualBudget.CarryForward", "ProjectAnnualBudget", "CarryForward",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Authorize carry forward", "اعتماد الترحيل", "Authorize explicit free-balance and outstanding-commitment carry forward during Project year-end closing", "تفويض ترحيل الرصيد الحر والالتزامات المحجوزة بموافقة صريحة. لا تُمنح هذه الصلاحية لأي دور تلقائياً.",
            "High", true, ["ProjectAnnualBudgetClosing.View"], true, PermissionScope.Project),
        new(
            "b0000174-0000-0000-0000-000000000174", "ProjectAnnualBudget.Close", "ProjectAnnualBudget", "Close",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Prepare closing", "إعداد الإقفال", "Prepare, submit, and cancel Project fiscal-year closing statements within the actor's authorized project scope", "إعداد أو إرسال أو إلغاء إقفال السنة المالية للمشروع ضمن نطاق المشروع المصرح به.",
            "Medium", true, ["ProjectAnnualBudgetClosing.View"], true, PermissionScope.Project),
        new(
            "b0000175-0000-0000-0000-000000000175", "ProjectAnnualBudget.CloseApprove", "ProjectAnnualBudget", "CloseApprove",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Review closing", "مراجعة الإقفال", "Approve or reject an assigned Project fiscal-year closing statement", "اعتماد أو رفض إقفال سنة مالية معيّن للمشروع. لا يجوز لمُعد الإقفال اعتماده.",
            "High", true, ["ProjectAnnualBudgetClosing.View"], true, PermissionScope.Project),
        new(
            "b0000177-0000-0000-0000-000000000177", "ProjectApproval.Override", "ProjectApproval", "Override",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Override approval", "تجاوز الموافقة", "Apply a reasoned exception to one approval submission only; does not change persistent Project Settings.", "تطبيق استثناء مسبب على إرسال واحد فقط، دون تغيير إعدادات المشروع الدائمة.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000173-0000-0000-0000-000000000173", "ProjectPaymentReversal.Approve", "ProjectPaymentReversal", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "settlements-and-reversals", "Settlements & Reversals", "التسويات وعمليات العكس", PermissionLevel.Action,
            "Approve or reject", "اعتماد أو رفض", "Approve or reject project Payment Request commitment reversals assigned within the actor's authorized project scope", "اعتماد أو رفض طلب مُسند لتحرير التزام غير مدفوع. لا تمنح هذه الصلاحية حق تسجيل التسويات أو إصدار المبالغ المستردة.",
            "High", true, [], true),
        new(
            "b0000172-0000-0000-0000-000000000172", "ProjectPaymentReversal.Create", "ProjectPaymentReversal", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "settlements-and-reversals", "Settlements & Reversals", "التسويات وعمليات العكس", PermissionLevel.Action,
            "Request reversal", "طلب عكس الالتزام", "Create and submit approved project Payment Request commitment reversals", "إنشاء وإرسال طلبات عكس الالتزام المالي غير المدفوع. لا تمنح هذه الصلاحية حق اعتماد طلبات العكس.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000237-0000-0000-0000-000000000237", "Report.View", "Report", "View",
            "reporting", "Reports & Insights", "التقارير والرؤى",
            "reports", "Reports", "التقارير", PermissionLevel.View,
            "View", "عرض", "View reports within verified tenant and organization query scope.", "عرض التقارير ضمن نطاق استعلام المستأجر والمؤسسة بعد التحقق منه.",
            "Low", false, [], true),
        new(
            "b0000017-0000-0000-0000-000000000017", "Resource.Assign", "Resource", "Assign",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Assign", "إسناد", "Assign resources to projects", "تنفيذ إجراء الموارد · إسناد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000024-0000-0000-0000-000000000024", "Resource.Create", "Resource", "Create",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Create", "إنشاء", "Create resources and vendors", "تنفيذ إجراء الموارد · إنشاء على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000026-0000-0000-0000-000000000026", "Resource.Delete", "Resource", "Delete",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Remove resources", "إزالة الموارد", "Deactivate or delete resources and vendors", "تنفيذ إجراء تعطيل الموارد والموردين أو حذفهم على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Resource.View"], true),
        new(
            "b0000025-0000-0000-0000-000000000025", "Resource.Edit", "Resource", "Edit",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Edit", "تعديل", "Edit resources and vendors", "تنفيذ إجراء الموارد · تعديل على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000022-0000-0000-0000-000000000022", "Resource.EditCostRate", "Resource", "EditCostRate",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Edit cost rates", "تعديل معدلات التكلفة", "Edit resource cost rates", "تنفيذ إجراء تعديل تكاليف الموارد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Resource.ViewCostRate", "Resource.View"], true),
        new(
            "b0000016-0000-0000-0000-000000000016", "Resource.View", "Resource", "View",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.View,
            "View", "عرض", "View resources", "تنفيذ إجراء الموارد · عرض على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000021-0000-0000-0000-000000000021", "Resource.ViewCostRate", "Resource", "ViewCostRate",
            "capacity", "Resources", "الموارد",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "View cost rates", "عرض معدلات التكلفة", "View resource cost rates", "تنفيذ إجراء عرض تكاليف الموارد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000112-0000-0000-0000-000000000112", "Risk.Create", "Risk", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "risk-management", "Risks", "المخاطر", PermissionLevel.Action,
            "Create", "إنشاء", "Create project risks", "تنفيذ إجراء المخاطر · إنشاء على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000114-0000-0000-0000-000000000114", "Risk.Delete", "Risk", "Delete",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "risk-management", "Risks", "المخاطر", PermissionLevel.Action,
            "Delete", "حذف", "Delete risk children", "تنفيذ إجراء المخاطر · حذف على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Risk.View"], true),
        new(
            "b0000113-0000-0000-0000-000000000113", "Risk.Edit", "Risk", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "risk-management", "Risks", "المخاطر", PermissionLevel.Action,
            "Edit", "تعديل", "Edit project risks and follow-through", "تنفيذ إجراء المخاطر · تعديل على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000115-0000-0000-0000-000000000115", "Risk.Mitigate", "Risk", "Mitigate",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "risk-management", "Risks", "المخاطر", PermissionLevel.Action,
            "Manage mitigation", "إدارة المعالجة", "Transition and mitigate project risks", "تنفيذ إجراء المخاطر · تخفيف المخاطر على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000111-0000-0000-0000-000000000111", "Risk.View", "Risk", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "risk-management", "Risks", "المخاطر", PermissionLevel.View,
            "View", "عرض", "View project risks", "تنفيذ إجراء المخاطر · عرض على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000238-0000-0000-0000-000000000238", "Settings.View", "Settings", "View",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant Settings", "إعدادات المستأجر", PermissionLevel.View,
            "View", "عرض", "View tenant and organization settings records within authorized tenant and management scope.", "عرض سجلات Tenant and organization settings ضمن نطاق المستأجر والإدارة المصرح به.",
            "Low", false, [], true),
        new(
            "b0000300-0000-0000-0000-000000000300", "EmailNotifications.View", "EmailNotifications", "View",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email Notifications", "إشعارات البريد الإلكتروني", PermissionLevel.View,
            "View", "عرض", "View effective SMTP settings and scoped email delivery records within authorized Tenant and Organization scope.", "عرض إعدادات SMTP الفعالة وسجلات تسليم البريد ضمن نطاق المستأجر والمؤسسة المصرح به.",
            "Medium", false, ["Settings.View"], true, PermissionScope.Tenant),
        new(
            "b0000133-0000-0000-0000-000000000133", "Stakeholders.Manage", "Stakeholders", "Manage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-stakeholders", "Stakeholders", "أصحاب المصلحة", PermissionLevel.Action,
            "Manage stakeholders", "إدارة أصحاب المصلحة", "Manage project stakeholders", "تنفيذ إجراء أصحاب المصلحة · إدارة على Project stakeholders مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Stakeholders.Read"], true),
        new(
            "b0000132-0000-0000-0000-000000000132", "Stakeholders.Read", "Stakeholders", "Read",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-stakeholders", "Stakeholders", "أصحاب المصلحة", PermissionLevel.View,
            "View", "عرض", "View project stakeholders", "تنفيذ إجراء أصحاب المصلحة · قراءة على Project stakeholders مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000158-0000-0000-0000-000000000158", "StatusReport.Create", "StatusReport", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Create", "إنشاء", "Create draft project status reports", "تنفيذ إجراء تقارير حالة المشروع · إنشاء على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000159-0000-0000-0000-000000000159", "StatusReport.Edit", "StatusReport", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Edit", "تعديل", "Edit draft project status reports", "تنفيذ إجراء تقارير حالة المشروع · تعديل على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000164-0000-0000-0000-000000000164", "StatusReport.Export", "StatusReport", "Export",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Export", "تصدير", "Export a project status report", "تنفيذ إجراء تقارير حالة المشروع · تصدير على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000165-0000-0000-0000-000000000165", "StatusReport.ManageConfiguration", "StatusReport", "ManageConfiguration",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Set reporting scope", "ضبط نطاق التقارير", "Configure official project reporting scope", "تنفيذ إجراء إدارة إعدادات تقارير الحالة على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000163-0000-0000-0000-000000000163", "StatusReport.Publish", "StatusReport", "Publish",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Publish", "نشر", "Publish a Draft status report and capture its immutable project snapshot", "تنفيذ إجراء تقارير حالة المشروع · نشر على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["StatusReport.View"], true),
        new(
            "b0000157-0000-0000-0000-000000000157", "StatusReport.View", "StatusReport", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "project-status-reports", "Project Status Reports", "تقارير حالة المشروع", PermissionLevel.View,
            "View", "عرض", "View project status reports and performance", "تنفيذ إجراء تقارير حالة المشروع · عرض على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000031-0000-0000-0000-000000000031", "Team.Create", "Team", "Create",
            "capacity", "Resources", "الموارد",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Create", "إنشاء", "Create teams", "تنفيذ إجراء الفرق · إنشاء على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Team.View"], true),
        new(
            "b0000033-0000-0000-0000-000000000033", "Team.Delete", "Team", "Delete",
            "capacity", "Resources", "الموارد",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Delete", "حذف", "Deactivate or delete teams", "تنفيذ إجراء الفرق · حذف على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Team.View"], true),
        new(
            "b0000032-0000-0000-0000-000000000032", "Team.Edit", "Team", "Edit",
            "capacity", "Resources", "الموارد",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Edit", "تعديل", "Edit teams and manage their members", "تنفيذ إجراء الفرق · تعديل على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Team.View"], true),
        new(
            "b0000030-0000-0000-0000-000000000030", "Team.View", "Team", "View",
            "capacity", "Resources", "الموارد",
            "teams", "Teams", "الفرق", PermissionLevel.View,
            "View", "عرض", "View teams and their members", "عرض الفرق التابعة للمؤسسة وسجل عضويتها محدد الفترات. هذه الصلاحية وحدها لا تمنح إسناد الموارد أو الوصول إلى واجهات المشروعات.",
            "Low", false, [], true),
        new(
            "b0000143-0000-0000-0000-000000000143", "TimeTracking.ManageOwn", "TimeTracking", "ManageOwn",
            "capacity", "Resources", "الموارد",
            "time-tracking", "Time Tracking", "تتبع الوقت", PermissionLevel.Action,
            "Manage own time", "إدارة الوقت الشخصي", "Create, update, and delete own manual time entries", "تنفيذ إجراء إدارة سجلات الوقت الشخصية على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["TimeTracking.ViewOwn"], true),
        new(
            "b0000145-0000-0000-0000-000000000145", "TimeTracking.ManageTeam", "TimeTracking", "ManageTeam",
            "capacity", "Resources", "الموارد",
            "time-tracking", "Time Tracking", "تتبع الوقت", PermissionLevel.Action,
            "Manage team time", "إدارة وقت الفريق", "Manage team manual time entries within management scope", "تنفيذ إجراء إدارة سجلات وقت الفريق على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["TimeTracking.ViewTeam"], true),
        new(
            "b0000142-0000-0000-0000-000000000142", "TimeTracking.ViewOwn", "TimeTracking", "ViewOwn",
            "capacity", "Resources", "الموارد",
            "time-tracking", "Time Tracking", "تتبع الوقت", PermissionLevel.View,
            "View own time", "عرض الوقت الشخصي", "View own manual time entries", "تنفيذ إجراء عرض سجلات الوقت الشخصية على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000144-0000-0000-0000-000000000144", "TimeTracking.ViewTeam", "TimeTracking", "ViewTeam",
            "capacity", "Resources", "الموارد",
            "time-tracking", "Time Tracking", "تتبع الوقت", PermissionLevel.View,
            "View team time", "عرض وقت الفريق", "View team manual time entries within management scope", "تنفيذ إجراء عرض سجلات وقت الفريق على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000239-0000-0000-0000-000000000239", "Users.View", "Users", "View",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.View,
            "View", "عرض", "View users and access records within authorized tenant and management scope.", "عرض سجلات Users and access ضمن نطاق المستأجر والإدارة المصرح به.",
            "Low", false, [], true),
        new(
            "b0000240-0000-0000-0000-000000000240", "Users.ResetPassword", "Users", "ResetPassword",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.Action,
            "Reset password", "إعادة تعيين كلمة المرور", "Generate a one-time temporary password for a local user", "تنفيذ إجراء إعادة تعيين كلمات مرور المستخدمين على Users and access مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", true, ["Users.View"], true),
        new(
            "b0000140-0000-0000-0000-000000000140", "Workflow.Manage", "Workflow", "Manage",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "workflow-administration", "Workflow Management", "إدارة سير العمل", PermissionLevel.Action,
            "Edit drafts", "تعديل المسودات", "Edit workflow drafts", "تنفيذ إجراء مسارات العمل · إدارة على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Workflow.View"], true),
        new(
            "b0000141-0000-0000-0000-000000000141", "Workflow.Publish", "Workflow", "Publish",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "workflow-administration", "Workflow Management", "إدارة سير العمل", PermissionLevel.Action,
            "Publish", "نشر", "Publish and retire workflow versions", "تنفيذ إجراء مسارات العمل · نشر على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Workflow.View"], true),
        new(
            "b0000139-0000-0000-0000-000000000139", "Workflow.View", "Workflow", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "workflow-administration", "Workflow Management", "إدارة سير العمل", PermissionLevel.View,
            "View", "عرض", "View workflow definitions and versions", "تنفيذ إجراء مسارات العمل · عرض على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000124-0000-0000-0000-000000000124", "WorkItem.Create", "WorkItem", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "work-items", "Work Items", "عناصر العمل", PermissionLevel.Action,
            "Create", "إنشاء", "Create task and issue work items", "تنفيذ إجراء عناصر العمل · إنشاء على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["WorkItem.View"], true),
        new(
            "b0000126-0000-0000-0000-000000000126", "WorkItem.Delete", "WorkItem", "Delete",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "work-items", "Work Items", "عناصر العمل", PermissionLevel.Action,
            "Delete", "حذف", "Delete task and issue work items", "تنفيذ إجراء عناصر العمل · حذف على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["WorkItem.View"], true),
        new(
            "b0000125-0000-0000-0000-000000000125", "WorkItem.Edit", "WorkItem", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "work-items", "Work Items", "عناصر العمل", PermissionLevel.Action,
            "Edit", "تعديل", "Edit task and issue work items", "تنفيذ إجراء عناصر العمل · تعديل على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["WorkItem.View"], true),
        new(
            "b0000123-0000-0000-0000-000000000123", "WorkItem.View", "WorkItem", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "work-items", "Work Items", "عناصر العمل", PermissionLevel.View,
            "View", "عرض", "View task and issue work items", "تنفيذ إجراء عناصر العمل · عرض على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000178-0000-0000-0000-000000000178", "ProjectAnnualBudget.View", "ProjectAnnualBudget", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.View,
            "View", "عرض", "Open the annual-budget workspace within tenant and project scope. Review-only users see assigned records only.", "فتح مساحة الميزانيات السنوية ضمن نطاق المستأجر والمشروع. يقتصر عرض المراجعين على السجلات المُسندة إليهم.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000179-0000-0000-0000-000000000179", "ProjectPaymentRequest.View", "ProjectPaymentRequest", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.View,
            "View", "عرض", "View requests and approval history in tenant/project scope; reviewer-only lists are assignment-filtered.", "عرض الطلبات وسجل اعتمادها ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد.",
            "High", true, [], true),
        new(
            "b0000180-0000-0000-0000-000000000180", "ProjectAnnualBudgetClosing.View", "ProjectAnnualBudgetClosing", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.View,
            "View", "عرض", "View closings in tenant/project scope; reviewer-only lists are assignment-filtered.", "عرض الإقفالات ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000233-0000-0000-0000-000000000233", "ProjectSettlement.View", "ProjectSettlement", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "settlements-and-reversals", "Settlements & Reversals", "التسويات وعمليات العكس", PermissionLevel.View,
            "View", "عرض", "View Project settlement, reversal, and planned-payment records within authorized Project scope. Assigned reviewers retain access only to their assigned workflow record.", "عرض سجلات تسويات المشروع وطلبات العكس والمدفوعات المخططة ضمن نطاق المشروع المصرح به. يقتصر وصول المراجعين على سجل الإجراء المسند إليهم.",
            "High", true, [], true),
        new(
            "b0000235-0000-0000-0000-000000000235", "ProjectFinancialAuditTrail.View", "ProjectFinancialAuditTrail", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "financial-audit-trail", "Financial Audit Trail", "سجل التدقيق المالي", PermissionLevel.View,
            "View", "عرض", "View immutable financial audit records within authorized Project scope. This permission never allows changing or deleting audit history.", "عرض سجلات التدقيق المالي غير القابلة للتعديل ضمن نطاق المشروع المصرح به. لا تسمح هذه الصلاحية بتعديل سجل التدقيق أو حذفه.",
            "High", true, [], true),
        new(
            "b0000268-0000-0000-0000-000000000268", "ProjectFinancialFoundation.View", "ProjectFinancialFoundation", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "financial-overview", "Overview", "نظرة عامة", PermissionLevel.View,
            "View overview", "عرض النظرة العامة", "Read the Project financial foundation summary, including the lifetime ceiling and source-backed actual-paid and outstanding-obligation totals when records are comparable. Legacy approved-payment allocation and available-budget fields may be null. Does not grant create, edit, approve, or export capabilities.", "قراءة ملخص الأساس المالي للمشروع، بما في ذلك السقف المالي مدى الحياة وإجماليات المدفوعات الفعلية والالتزامات المستحقة المدعومة بسجلات المصدر عندما تكون السجلات قابلة للمقارنة. قد لا تتوفر حقول تخصيص مدفوعات الطلبات المعتمدة والميزانية المتاحة. لا يمنح هذا الإذن صلاحيات الإنشاء أو التعديل أو الاعتماد أو التصدير.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000270-0000-0000-0000-000000000270", "ProgramFinancialRollup.View", "ProgramFinancialRollup", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "program-financial-rollup", "Program Financials", "ماليات البرنامج", PermissionLevel.View,
            "View", "عرض", "Read a Program financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Program/descendant Project management scope. Does not grant Project workflow actions or unscoped Project access.", "عرض التجميع المالي للبرنامج وتفاصيل المشاريع الموزعة على الصفحات، مع الاقتصار على المشاريع ضمن المستأجر الحالي ونطاق الإدارة المصرح به للبرنامج والمشاريع التابعة له. لا تمنح هذه الصلاحية إجراءات سير العمل المالية للمشروع أو الوصول إلى مشاريع خارج النطاق.",
            "High", true, [], true, PermissionScope.Program),
        new(
            "b0000271-0000-0000-0000-000000000271", "PortfolioFinancialRollup.View", "PortfolioFinancialRollup", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "portfolio-financial-rollup", "Portfolio Financials", "ماليات المحفظة", PermissionLevel.View,
            "View", "عرض", "Read a Portfolio financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Portfolio/descendant Program and Project management scope. Does not grant Project workflow actions or unscoped Project access.", "عرض التجميع المالي للمحفظة وتفاصيل المشاريع الموزعة على الصفحات، مع الاقتصار على المشاريع ضمن المستأجر الحالي ونطاق الإدارة المصرح به للمحفظة والبرامج والمشاريع التابعة لها. لا تمنح هذه الصلاحية إجراءات سير العمل المالية للمشروع أو الوصول إلى مشاريع خارج النطاق.",
            "High", true, [], true, PermissionScope.Portfolio),
        new(
            "b0000181-0000-0000-0000-000000000181", "Dashboard.View", "Dashboard", "View",
            "reporting", "Reports & Insights", "التقارير والرؤى",
            "home-dashboards", "Home", "الرئيسية", PermissionLevel.View,
            "View", "عرض", "Open the dashboard workspace. Each widget data API continues to enforce its own permission and tenant/organization scope.", "فتح مساحة لوحات المعلومات، مع استمرار كل واجهة بيانات لعنصر لوحة بعينه في تطبيق صلاحياتها ونطاق المستأجر والمؤسسة.",
            "Medium", false, [], true),
        new(
            "b0000182-0000-0000-0000-000000000182", "ProjectAnnualBudget.Create", "ProjectAnnualBudget", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Create draft", "إنشاء مسودة", "Create an annual-budget draft within project and fiscal-year scope.", "إنشاء مسودة ميزانية سنوية ضمن نطاق المشروع والسنة المالية.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000183-0000-0000-0000-000000000183", "ProjectAnnualBudget.EditDraft", "ProjectAnnualBudget", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Edit draft", "تعديل المسودة", "Edit an eligible draft before submission.", "تعديل مسودة مستوفية للشروط قبل إرسالها.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000306-0000-0000-0000-000000000306", "ProjectAnnualBudget.ConfigureDraftApproval", "ProjectAnnualBudget", "ConfigureDraftApproval",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Configure approval", "تهيئة الموافقة", "Configure and save approvers and review order for this Annual Budget Draft. The configuration applies to its next submission only and does not change Project Settings or an immutable submitted approval plan.", "تهيئة قائمة المعتمدين وترتيب المراجعة لمسودة الميزانية السنوية الحالية. يُطبق الإعداد عند إرسالها التالية فقط، ولا يغير إعدادات المشروع أو خطة اعتماد مُرسلة وغير قابلة للتعديل.",
            "High", true, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.EditDraft"], true, PermissionScope.Project),
        new(
            "b0000184-0000-0000-0000-000000000184", "ProjectAnnualBudget.SubmitForReview", "ProjectAnnualBudget", "SubmitForReview",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Submit for review", "إرسال للمراجعة", "Submit an eligible draft to its configured review workflow.", "إرسال مسودة مستوفية للشروط إلى مسار المراجعة المحدد.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000185-0000-0000-0000-000000000185", "ProjectAnnualBudget.ReviseApproved", "ProjectAnnualBudget", "ReviseApproved",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Revise approved budget", "تعديل الميزانية المعتمدة", "Submit an eligible revision to an approved budget.", "إرسال تعديل مستوفٍ للشروط على ميزانية معتمدة.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000186-0000-0000-0000-000000000186", "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget", "ManageApprovalConfiguration",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budget-defaults", "Annual Budget Approval Defaults", "الإعدادات الافتراضية لاعتماد الموازنة السنوية", PermissionLevel.Action,
            "Set approval defaults", "ضبط إعدادات الاعتماد الافتراضية", "Change persistent Project Settings approval defaults; distinct from a one-submission override.", "تغيير إعدادات الاعتماد الافتراضية الدائمة للمشروع، بمعزل عن تجاوز إرسال واحد.",
            "Critical", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000304-0000-0000-0000-000000000304", "ProjectAnnualBudget.Delete", "ProjectAnnualBudget", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Delete draft", "حذف المسودة", "Delete an eligible unapproved Draft annual budget with no active approval workflow or protected financial dependency.", "حذف مسودة موازنة سنوية غير معتمدة ومؤهلة، بشرط عدم وجود مراجعة موافقة نشطة أو ارتباط مالي محمي.",
            "Critical", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000305-0000-0000-0000-000000000305", "ProjectAnnualBudget.Withdraw", "ProjectAnnualBudget", "Withdraw",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Withdraw submission", "سحب الإرسال", "Withdraw a Pending Approval Annual Budget submission and cancel its active approval request only when the workflow permits; preserve the prior approval history.", "سحب إرسال ميزانية سنوية قيد المراجعة وإلغاء طلب الاعتماد النشط عندما يسمح مسار العمل بذلك، مع الاحتفاظ بسجل الاعتماد السابق.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project),
        new(
            "b0000187-0000-0000-0000-000000000187", "ProjectPaymentRequest.Create", "ProjectPaymentRequest", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Create request", "إنشاء طلب", "Create a request draft within project scope and currency rules.", "إنشاء مسودة طلب ضمن نطاق المشروع وقواعد العملة.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000188-0000-0000-0000-000000000188", "ProjectPaymentRequest.EditDraft", "ProjectPaymentRequest", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Edit draft", "تعديل المسودة", "Edit an eligible request draft before submission.", "تعديل مسودة طلب مستوفية للشروط قبل إرسالها.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000189-0000-0000-0000-000000000189", "ProjectPaymentRequest.Submit", "ProjectPaymentRequest", "Submit",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Submit", "إرسال", "Submit an eligible request to its approval workflow.", "إرسال طلب مستوفٍ للشروط إلى مسار الاعتماد.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000190-0000-0000-0000-000000000190", "ProjectPaymentRequest.Cancel", "ProjectPaymentRequest", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Cancel", "إلغاء", "Cancel an eligible request under workflow-state rules; distinct from reversing settlement.", "إلغاء طلب مستوفٍ للشروط وفق حالته في سير العمل؛ وهو مستقل عن عكس التسوية.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000191-0000-0000-0000-000000000191", "ProjectPaymentRequest.Approve", "ProjectPaymentRequest", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Approve or reject", "اعتماد أو رفض", "Decide only an assigned request; active team membership, project scope, exact reviewer assignment, and separation of duties remain required.", "اتخاذ قرار في طلب مُسند فقط؛ مع استمرار اشتراط عضوية الفريق ونطاق المشروع والإسناد الدقيق والفصل بين المهام.",
            "Critical", true, [], true),
        new(
            "b0000192-0000-0000-0000-000000000192", "Program.CreateAcrossPortfolios", "Program", "CreateAcrossPortfolios",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Create across portfolios", "إنشاء عبر المحافظ", "Allow creation outside native Portfolio assignment only within eligible tenant and organization scope; still requires Program.Create.", "السماح بالإنشاء خارج إسناد المحفظة الأصلي داخل نطاق المستأجر والمؤسسة المؤهل، مع استمرار اشتراط Program.Create.",
            "Critical", true, ["Program.View", "Portfolio.View", "Program.Create"], true, PermissionScope.Organization),
        new(
            "b0000193-0000-0000-0000-000000000193", "Users.ManageAccounts", "Users", "ManageAccounts",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.Action,
            "Manage accounts", "إدارة الحسابات", "Create users and update profile details. Account activation/deactivation requires Users.SetStatus. Excludes role assignment, permission overrides, and password reset.", "إنشاء حسابات المستخدمين وتعديل بياناتهم. يتطلب تفعيل الحساب أو تعطيله صلاحية Users.SetStatus. لا تشمل هذه الصلاحية إسناد الأدوار أو تجاوزات الصلاحيات أو إعادة تعيين كلمة المرور.",
            "High", true, ["Users.View"], true),
        new(
            "b0000217-0000-0000-0000-000000000217", "Users.SetStatus", "Users", "SetStatus",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.Action,
            "Change status", "تغيير الحالة", "Activate or deactivate a tenant user account. This does not grant profile editing, role assignment, direct permission overrides, or password reset.", "تفعيل حساب مستخدم في المستأجر أو تعطيله. لا تمنح هذه الصلاحية تعديل بيانات الحساب أو إسناد الأدوار أو تجاوزات الصلاحيات المباشرة أو إعادة تعيين كلمة المرور.",
            "High", true, ["Users.View"], true),
        new(
            "b0000195-0000-0000-0000-000000000195", "Users.AssignRoles", "Users", "AssignRoles",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.Action,
            "Assign roles", "إسناد الأدوار", "Assign/remove tenant roles; changes effective access and require audit.", "إسناد أدوار المستأجر أو إزالتها؛ وهي تغييرات تؤثر على الوصول الفعلي وتتطلب التدقيق.",
            "Critical", true, ["Users.View"], true),
        new(
            "b0000196-0000-0000-0000-000000000196", "UserPermissions.Manage", "UserPermissions", "Manage",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users", "المستخدمون", PermissionLevel.Action,
            "Manage direct access", "إدارة الوصول المباشر", "Add/remove per-user Allow/Deny overrides with audit and delegation limits.", "إضافة أو إزالة تجاوزات السماح أو المنع للمستخدم مع التدقيق وحدود التفويض.",
            "Critical", true, ["Users.View"], true),
        new(
            "b0000197-0000-0000-0000-000000000197", "Settings.ManageTenantConfiguration", "Settings", "ManageTenantConfiguration",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant Settings", "إعدادات المستأجر", PermissionLevel.Action,
            "Edit tenant settings", "تعديل إعدادات المستأجر", "Update tenant settings permitted by typed definitions; platform settings remain separate.", "تحديث إعدادات المستأجر المسموح بها وفق التعريفات؛ وتظل إعدادات المنصة منفصلة.",
            "High", true, ["Settings.View"], true),
        new(
            "b0000317-0000-0000-0000-000000000317", "OrganizationConfiguration.View", "OrganizationConfiguration", "View",
            "administration", "Administration", "الإدارة",
            "organization-configuration", "Organization Configuration", "إعدادات المؤسسة", PermissionLevel.View,
            "View", "عرض", "View supported Organization-level configuration within the authorized Organization scope.", "عرض إعدادات المؤسسة المدعومة ضمن نطاق المؤسسة المصرح به.",
            "High", true, [], true, PermissionScope.Organization),
        new(
            "b0000318-0000-0000-0000-000000000318", "OrganizationConfiguration.Manage", "OrganizationConfiguration", "Manage",
            "administration", "Administration", "الإدارة",
            "organization-configuration", "Organization Configuration", "إعدادات المؤسسة", PermissionLevel.Action,
            "Manage", "إدارة", "Manage eligible Organization-level configuration within the authorized Organization scope. Tenant-enforced values remain read-only.", "إدارة إعدادات المؤسسة المسموح بها ضمن نطاق المؤسسة المصرح به. تظل القيم المفروضة على مستوى المستأجر للقراءة فقط.",
            "Critical", true, ["OrganizationConfiguration.View"], true, PermissionScope.Organization),
        new(
            "b0000301-0000-0000-0000-000000000301", "EmailNotifications.Manage", "EmailNotifications", "Manage",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email Notifications", "إشعارات البريد الإلكتروني", PermissionLevel.Action,
            "Manage settings", "إدارة الإعدادات", "Create, update, and reset scoped SMTP profiles. Credentials remain protected and are never returned.", "إنشاء ملفات SMTP وتعديلها وإعادة ضبطها ضمن النطاق المصرح به. تظل بيانات الاعتماد محمية ولا يتم إرجاعها.",
            "Critical", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000302-0000-0000-0000-000000000302", "EmailNotifications.Test", "EmailNotifications", "Test",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email Notifications", "إشعارات البريد الإلكتروني", PermissionLevel.Action,
            "Test", "اختبار", "Test the scoped SMTP connection or send a test only to the authenticated actor's active account address.", "اختبار اتصال SMTP ضمن النطاق أو إرسال رسالة اختبار إلى عنوان الحساب النشط للمستخدم الحالي فقط.",
            "High", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000303-0000-0000-0000-000000000303", "EmailNotifications.Logs", "EmailNotifications", "Logs",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email Notifications", "إشعارات البريد الإلكتروني", PermissionLevel.View,
            "View delivery logs", "عرض سجلات الإرسال", "View scoped email event, attempt, and provider-acceptance status without message bodies or credentials.", "عرض أحداث البريد ومحاولاته وحالة قبول الخادم ضمن النطاق، دون محتوى الرسائل أو بيانات الاعتماد.",
            "Medium", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000199-0000-0000-0000-000000000199", "Program.Create", "Program", "Create",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Create", "إنشاء", "Create a Program under an eligible Portfolio. Native assignment is required unless the explicit cross-Portfolio capability is also granted.", "إنشاء برنامج تحت محفظة مؤهلة. يلزم إسناد المحفظة ما لم تُمنح صلاحية الإنشاء عبر المحافظ صراحةً.",
            "High", true, ["Program.View"], true),
        new(
            "b0000200-0000-0000-0000-000000000200", "Program.Edit", "Program", "Edit",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Edit", "تعديل", "Edit an existing Program within native management scope.", "تعديل برنامج قائم ضمن نطاق الإدارة الأصلي.",
            "High", true, ["Program.View"], true),
        new(
            "b0000201-0000-0000-0000-000000000201", "Program.Archive", "Program", "Archive",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Archive", "أرشفة", "Archive a Program within native management scope.", "أرشفة برنامج ضمن نطاق الإدارة الأصلي.",
            "High", true, ["Program.View"], true),
        new(
            "b0000202-0000-0000-0000-000000000202", "Program.Reactivate", "Program", "Reactivate",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Reactivate", "إعادة التفعيل", "Reactivate an archived Program within native management scope.", "إعادة تفعيل برنامج مؤرشف ضمن نطاق الإدارة الأصلي.",
            "Medium", true, ["Program.View"], true),
        new(
            "b0000203-0000-0000-0000-000000000203", "Portfolio.Create", "Portfolio", "Create",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Create", "إنشاء", "Create a Portfolio within tenant and organization authority.", "إنشاء محفظة ضمن صلاحيات المستأجر والمؤسسة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000204-0000-0000-0000-000000000204", "Portfolio.Edit", "Portfolio", "Edit",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Edit", "تعديل", "Edit a Portfolio within tenant and organization authority.", "تعديل محفظة ضمن صلاحيات المستأجر والمؤسسة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000205-0000-0000-0000-000000000205", "Portfolio.Archive", "Portfolio", "Archive",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Archive", "أرشفة", "Archive a Portfolio subject to tenant, organization, and lifecycle checks.", "أرشفة محفظة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000206-0000-0000-0000-000000000206", "Portfolio.Reactivate", "Portfolio", "Reactivate",
            "portfolio-program-management", "Delivery Management", "إدارة التسليم",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Reactivate", "إعادة التفعيل", "Reactivate an archived Portfolio subject to tenant, organization, and lifecycle checks.", "إعادة تفعيل محفظة مؤرشفة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة.",
            "Medium", true, ["Portfolio.View"], true),
        new(
            "b0000207-0000-0000-0000-000000000207", "ProjectFinancialCeiling.ApproveChange", "ProjectFinancialCeiling", "ApproveChange",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "change-requests", "Change Requests", "طلبات التغيير", PermissionLevel.Action,
            "Approve ceiling change", "اعتماد تغيير السقف المالي", "Qualify approval of a change request that alters the approved lifetime ceiling. Also requires the normal ChangeRequest.Approve decision capability, exact workflow assignment, project scope, and separation of duties.", "تقييد اعتماد طلب تغيير يبدل السقف المالي المعتمد طوال عمر المشروع. يتطلب أيضًا صلاحية قرار ChangeRequest.Approve والإسناد الدقيق في سير العمل ونطاق المشروع والفصل بين المهام.",
            "Critical", true, ["ChangeRequest.Approve"], true),
        new(
            "b0000212-0000-0000-0000-000000000212", "Roles.View", "Roles", "View",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles & Permissions", "الأدوار والصلاحيات", PermissionLevel.View,
            "View", "عرض", "Open the tenant role and permission workspace. Role data remains tenant-scoped and delegation-limited.", "فتح مساحة الأدوار والصلاحيات الخاصة بالمستأجر مع الالتزام بنطاق المستأجر وحدود تفويض الصلاحيات.",
            "Critical", true, [], true),
        new(
            "b0000213-0000-0000-0000-000000000213", "ReferenceData.View", "ReferenceData", "View",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference Data", "البيانات المرجعية", PermissionLevel.View,
            "View", "عرض", "Open tenant-scoped reference data and lookup workspaces.", "فتح مساحات البيانات المرجعية والقوائم المساندة ضمن نطاق المستأجر.",
            "Critical", true, [], true),
        new(
            "b0000225-0000-0000-0000-000000000225", "ReferenceData.Create", "ReferenceData", "Create",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference Data", "البيانات المرجعية", PermissionLevel.Action,
            "Create", "إنشاء", "Create supported tenant-managed reference data, including currencies, vendor categories, work-item statuses, and Project Roles.", "إنشاء البيانات المرجعية التي يديرها المستأجر، بما في ذلك العملات وفئات الموردين وحالات عناصر العمل وأدوار المشروع.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000226-0000-0000-0000-000000000226", "ReferenceData.Edit", "ReferenceData", "Edit",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference Data", "البيانات المرجعية", PermissionLevel.Action,
            "Edit", "تعديل", "Edit supported tenant-managed reference data while preserving category-specific immutability and usage rules.", "تعديل البيانات المرجعية التي يديرها المستأجر مع احترام قواعد عدم التغيير والاستخدام الخاصة بكل فئة.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000227-0000-0000-0000-000000000227", "ReferenceData.Delete", "ReferenceData", "Delete", "administration", "Administration", "الإدارة",
            "reference-data", "Reference Data", "البيانات المرجعية", PermissionLevel.Action,
            "Delete or deactivate", "حذف أو تعطيل", "Soft-delete currencies or deactivate supported lookup values where existing business rules permit. Referenced or immutable values remain protected.", "حذف العملات حذفًا منطقيًا أو إيقاف القيم المرجعية التي تدعمها القواعد الحالية. تظل القيم المستخدمة أو غير القابلة للتغيير محمية.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000214-0000-0000-0000-000000000214", "TenantAdministration.ManageScope", "TenantAdministration", "ManageScope",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant Settings", "إعدادات المستأجر", PermissionLevel.Action,
            "Manage access scope", "إدارة نطاق الوصول", "Grant tenant-wide administrative management scope within this tenant only. Does not grant platform or cross-tenant authority.", "منح نطاق الإدارة على مستوى هذا المستأجر فقط، دون صلاحيات على المنصة أو على مستأجرين آخرين.",
            "Critical", true, ["Settings.View"], true),

        new(
            "b0000208-0000-0000-0000-000000000208", "Roles.Create", "Roles", "Create",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles & Permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Create", "إنشاء", "Create a tenant-scoped role. Assigning permissions also requires Roles.ConfigurePermissions and may not exceed the actor's delegated permissions.", "إنشاء دور ضمن المستأجر. يتطلب إسناد الصلاحيات أيضاً Roles.ConfigurePermissions، ولا يجوز أن يتجاوز الصلاحيات المفوضة للمستخدم.",
            "High", true, ["Roles.View"], true),
        new(
            "b0000209-0000-0000-0000-000000000209", "Roles.EditDetails", "Roles", "EditDetails",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles & Permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Edit details", "تعديل التفاصيل", "Change a role's name, description, and category without changing its permission grants.", "تعديل اسم الدور ووصفه وفئته دون تغيير الصلاحيات الممنوحة له.",
            "Medium", false, ["Roles.View"], true),
        new(
            "b0000210-0000-0000-0000-000000000210", "Roles.ConfigurePermissions", "Roles", "ConfigurePermissions",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles & Permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Edit permissions", "تعديل الصلاحيات", "Assign or remove permissions within the actor's effective delegation authority; never permits self-escalation.", "إسناد الصلاحيات أو إزالتها ضمن حدود التفويض الفعلي للمستخدم، دون السماح برفع صلاحياته بنفسه.",
            "Critical", true, ["Roles.View"], true),
        new(
            "b0000211-0000-0000-0000-000000000211", "Roles.Delete", "Roles", "Delete",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles & Permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Delete", "حذف", "Delete an unused tenant role. System roles and roles assigned to users cannot be deleted.", "حذف دور غير مستخدم ضمن المستأجر. لا يمكن حذف أدوار النظام أو الأدوار المسندة إلى مستخدمين.",
            "High", true, ["Roles.View"], true),
        new(
            "b0000241-0000-0000-0000-000000000241", "Issue.View", "Issue", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "issues", "Issues", "المشكلات", PermissionLevel.View,
            "View", "عرض", "Read tenant issues within the user's assigned project scope.", "عرض مشكلات المستأجر ضمن نطاق المشاريع المسندة للمستخدم.",
            "Low", false, [], true, PermissionScope.Tenant),
        new(
            "b0000242-0000-0000-0000-000000000242", "Plugin.View", "Plugin", "View",
            "administration", "Administration", "الإدارة",
            "plugins", "Plugins", "الإضافات", PermissionLevel.View,
            "View", "عرض", "Read the current tenant's plugin registry and activation metadata.", "عرض سجل إضافات المستأجر وبيانات التفعيل.",
            "Low", false, [], true, PermissionScope.Tenant),
        new(
            "b0000236-0000-0000-0000-000000000236", "Directory.ManageConnection", "Directory", "ManageConnection",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory Integration", "تكامل الدليل", PermissionLevel.Action,
            "Manage connections", "إدارة الاتصالات", "Create and update directory connections and synchronization scope within the current tenant.", "إنشاء اتصالات الدليل وتحديثها ونطاق المزامنة ضمن المستأجر الحالي.",
            "Medium", false, ["Directory.View"], true, PermissionScope.Tenant),
        new(
            "b0000243-0000-0000-0000-000000000243", "Planning.Edit", "Planning", "Edit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "planning", "Project Plan", "خطة المشروع", PermissionLevel.Action,
            "Edit", "تعديل", "Edit existing plan activities, dependencies, assignments, and calendars within project scope.", "تعديل الأنشطة والارتباطات والتعيينات والتقاويم الموجودة في الخطة ضمن نطاق المشروع.",
            "High", true, ["Planning.View"], true, PermissionScope.Project),
        new(
            "b0000286-0000-0000-0000-000000000286", "ProjectPlan.Create", "ProjectPlan", "Create",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "planning", "Project Plan", "خطة المشروع", PermissionLevel.Action,
            "Create plan", "إنشاء خطة", "Create a Project Plan within the actor's authorized project scope.", "إنشاء خطة مشروع ضمن نطاق المشروع المصرح به للمستخدم.",
            "High", true, ["Planning.View"], true, PermissionScope.Project),
        new(
            "b0000272-0000-0000-0000-000000000272", "ProjectSettings.View", "ProjectSettings", "View",
            "project-management", "Delivery Management", "إدارة التسليم",
            "settings", "Project Settings", "إعدادات المشروع", PermissionLevel.View,
            "View", "عرض", "Open Project Settings within the actor's authorized Tenant and Project scope. This permission does not allow saving changes or replace permissions required for protected settings.", "عرض إعدادات المشروع ضمن نطاق المستأجر والمشروع المصرح به للمستخدم. لا تمنح هذه الصلاحية حفظ التغييرات أو تحل محل الصلاحيات المطلوبة للإعدادات المحمية.",
            "Low", false, [], true, PermissionScope.Project),
        new(
            "b0000273-0000-0000-0000-000000000273", "ProjectSettings.Edit", "ProjectSettings", "Edit",
            "project-management", "Delivery Management", "إدارة التسليم",
            "settings", "Project Settings", "إعدادات المشروع", PermissionLevel.Action,
            "Edit", "تعديل", "Edit Project Settings within the actor's authorized Tenant and Project scope. Financial approval defaults and other protected settings continue to require their specific permissions.", "تعديل إعدادات المشروع ضمن نطاق المستأجر والمشروع المصرح به للمستخدم. تظل الإعدادات المالية المحمية وغيرها خاضعة للصلاحيات المخصصة لها.",
            "Medium", true, ["ProjectSettings.View"], true, PermissionScope.Project),
        new(
            "b0000280-0000-0000-0000-000000000280", "ApprovalRequest.View", "ApprovalRequest", "View",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.View,
            "View", "عرض", "View approval requests within tenant and project scope; assignment-filtered for reviewers.", "عرض طلبات الاعتماد ضمن نطاق المستأجر والمشروع؛ تُصفى قوائم المراجعين حسب الإسناد.",
            "Low", false, [], true),
        new(
            "b0000281-0000-0000-0000-000000000281", "ApprovalRequest.Submit", "ApprovalRequest", "Submit",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.Action,
            "Submit", "إرسال", "Submit a draft approval request to its workflow.", "إرسال مسودة طلب اعتماد إلى مساره المعتمد.",
            "Medium", true, ["ApprovalRequest.View"], true),
        new(
            "b0000282-0000-0000-0000-000000000282", "ApprovalRequest.Approve", "ApprovalRequest", "Approve",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.Action,
            "Approve assigned", "اعتماد الطلبات المسندة", "Approve an assigned approval request; separation of duties applies.", "اعتماد طلب اعتماد مُسند؛ يطبق الفصل بين المهام.",
            "High", true, ["ApprovalRequest.View"], true),
        new(
            "b0000283-0000-0000-0000-000000000283", "ApprovalRequest.Reject", "ApprovalRequest", "Reject",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.Action,
            "Reject", "رفض", "Reject an assigned approval request with a reason.", "رفض طلب اعتماد مُسند مع إبداء سبب.",
            "High", true, ["ApprovalRequest.View"], true),
        new(
            "b0000284-0000-0000-0000-000000000284", "ApprovalRequest.Delegate", "ApprovalRequest", "Delegate",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.Action,
            "Delegate", "تفويض", "Delegate an assigned approval request to another user.", "تفويض طلب اعتماد مُسند إلى مستخدم آخر.",
            "Medium", true, ["ApprovalRequest.View"], true),
        new(
            "b0000285-0000-0000-0000-000000000285", "ApprovalRequest.Cancel", "ApprovalRequest", "Cancel",
            "project-delivery", "Delivery Management", "إدارة التسليم",
            "approvals", "Approvals", "الموافقات", PermissionLevel.Action,
            "Cancel", "إلغاء", "Cancel a draft or submitted approval request.", "إلغاء طلب اعتماد مسود أو مُرسل.",
            "Medium", true, ["ApprovalRequest.View"], true),
    ];

    public static IReadOnlyList<PermissionCatalogEntry> Entries => Catalog;

    public static IReadOnlyDictionary<string, PermissionCatalogEntry> ByKey { get; } = Catalog.ToDictionary(item => item.Key, StringComparer.Ordinal);

    public static IReadOnlySet<string> EnforceDependencies(IEnumerable<string> permissionKeys)
    {
        // Unknown and inactive persisted keys are not effective permissions.
        var effective = permissionKeys
            .Where(key => ByKey.TryGetValue(key, out var entry) && entry.IsActive)
            .ToHashSet(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var entry in Catalog.Where(item => item.Level == PermissionLevel.Action
                && effective.Contains(item.Key)))
            {
                if (entry.RequiredPermissionKeys.All(effective.Contains)) continue;
                effective.Remove(entry.Key);
                changed = true;
            }
        } while (changed);

        return effective;
    }

    public static IReadOnlyList<Permission> CreateSeedPermissions(Guid tenantId) => Catalog
        .Where(item => item.IsActive)
        .Select(item => new Permission
        {
            Id = Guid.Parse(item.SeedId), TenantId = tenantId, Name = item.Key, Resource = item.Resource, Action = item.Action,
            Description = item.DescriptionEn, CreatedBy = "System"
        }).ToList();

    public static bool HasUniqueKeys() => Catalog.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() == Catalog.Count;
}

