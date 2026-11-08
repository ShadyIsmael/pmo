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
    bool IsActive, PermissionScope Scope = PermissionScope.Tenant, bool IsAssignable = true,
    bool IsTenantAdminBaseline = false)
{
    public IReadOnlyList<string> AllowedCategories => PermissionRoleCategoryPolicy.GetAllowedCategories(Key);
}

public static class PermissionCatalog
{
    private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog =
    [
        new(
            "b0000169-0000-0000-0000-000000000169", "Audit.View", "Audit", "View",
            "administration", "Administration", "الإدارة",
            "audit-trail", "Audit trail", "سجل التدقيق", PermissionLevel.View,
            "View audit trail", "عرض سجل التدقيق", "Read the tenant audit trail and governance activity log", "تنفيذ إجراء عرض سجل التدقيق على Audit trail مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000107-0000-0000-0000-000000000107", "ChangeRequest.Approve", "ChangeRequest", "Approve",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "change-requests", "Change requests", "طلبات التغيير", PermissionLevel.Action,
            "Change request · Approve or reject", "طلب التغيير · اعتماد أو رفض", "Approve or reject project change requests", "تنفيذ إجراء طلب التغيير · اعتماد أو رفض على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, [], true),
        new(
            "b0000106-0000-0000-0000-000000000106", "ChangeRequest.Manage", "ChangeRequest", "Manage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "change-requests", "Change requests", "طلبات التغيير", PermissionLevel.Action,
            "Create, update, and cancel change requests", "إنشاء طلبات التغيير وتعديلها وإلغاؤها", "Create, update, and cancel project change requests", "تنفيذ إجراء إنشاء طلبات التغيير وتعديلها وإلغاؤها على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["ChangeRequest.View"], true),
        new(
            "b0000105-0000-0000-0000-000000000105", "ChangeRequest.View", "ChangeRequest", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "change-requests", "Change requests", "طلبات التغيير", PermissionLevel.View,
            "Change requests · View", "طلبات التغيير · عرض", "View project change requests", "تنفيذ إجراء طلبات التغيير · عرض على Change requests مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000154-0000-0000-0000-000000000154", "Directory.ExecuteSync", "Directory", "ExecuteSync",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Run directory synchronization", "تنفيذ مزامنة الدليل", "Execute selected directory synchronization actions", "تنفيذ إجراء تنفيذ مزامنة الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Directory.View"], true),
        new(
            "b0000155-0000-0000-0000-000000000155", "Directory.ManageMappings", "Directory", "ManageMappings",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Manage directory mappings", "إدارة تعيينات الدليل", "Manage directory field, group, and organization mappings", "تنفيذ إجراء إدارة تعيينات الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000156-0000-0000-0000-000000000156", "Directory.ManageProvisioningPolicy", "Directory", "ManageProvisioningPolicy",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Manage provisioning policy", "إدارة سياسة إنشاء الحسابات", "Manage directory provisioning policy", "تنفيذ إجراء إدارة سياسة إنشاء الحسابات على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Directory.View"], true),
        new(
            "b0000153-0000-0000-0000-000000000153", "Directory.PreviewSync", "Directory", "PreviewSync",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Preview directory synchronization", "معاينة مزامنة الدليل", "Preview directory synchronization", "تنفيذ إجراء معاينة مزامنة الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000152-0000-0000-0000-000000000152", "Directory.TestConnection", "Directory", "TestConnection",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Test directory connection", "اختبار اتصال الدليل", "Test directory connectivity", "تنفيذ إجراء اختبار اتصال الدليل على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Directory.View"], true),
        new(
            "b0000150-0000-0000-0000-000000000150", "Directory.View", "Directory", "View",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.View,
            "Directory integration · View", "تكامل الدليل · عرض", "View directory integration configuration and sync history", "تنفيذ إجراء تكامل الدليل · عرض على Directory integration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000137-0000-0000-0000-000000000137", "Document.Archive", "Document", "Archive",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Documents · Archive", "المستندات · أرشفة", "Archive documents", "تنفيذ إجراء المستندات · أرشفة على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", true, ["Document.View"], true),
        new(
            "b0000135-0000-0000-0000-000000000135", "Document.Create", "Document", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Documents · Create", "المستندات · إنشاء", "Upload documents", "تنفيذ إجراء المستندات · إنشاء على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000136-0000-0000-0000-000000000136", "Document.Edit", "Document", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Documents · Edit", "المستندات · تعديل", "Upload document versions", "تنفيذ إجراء المستندات · تعديل على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000138-0000-0000-0000-000000000138", "Document.TemplateManage", "Document", "TemplateManage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "documents", "Documents", "المستندات", PermissionLevel.Action,
            "Manage document templates", "إدارة قوالب المستندات", "Manage reusable document templates", "تنفيذ إجراء إدارة قوالب المستندات على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Document.View"], true),
        new(
            "b0000134-0000-0000-0000-000000000134", "Document.View", "Document", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "documents", "Documents", "المستندات", PermissionLevel.View,
            "Documents · View", "المستندات · عرض", "View documents and templates", "تنفيذ إجراء المستندات · عرض على Documents مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000228-0000-0000-0000-000000000228", "ProjectFinance.View", "ProjectFinance", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "project-finance", "Project Finance", "مالية المشروع", PermissionLevel.View,
            "Open Project Finance", "فتح مساحة مالية المشروع", "Open the Project Finance navigation shell. Each finance workspace has its own data View permission and remains separately scoped.", "فتح هيكل التنقل لمساحة مالية المشروع. لكل مساحة مالية صلاحية عرض بيانات مستقلة وتظل خاضعة لنطاقها الخاص.",
            "Low", false, [], true),
        new(
            "b0000244-0000-0000-0000-000000000244", "Budget.View", "Budget", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.View,
            "View budgets", "عرض الميزانيات", "View legacy budget records within authorized tenant and project or portfolio management scope.", "عرض سجلات الميزانية ضمن نطاق المستأجر والمشروع أو المحفظة المصرح به.",
            "High", true, [], true),
        new(
            "b0000245-0000-0000-0000-000000000245", "Budget.Create", "Budget", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Create budgets", "إنشاء ميزانيات", "Create legacy budget records within authorized project or portfolio scope.", "إنشاء سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["Budget.View"], true),
        new(
            "b0000246-0000-0000-0000-000000000246", "Budget.Edit", "Budget", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Edit budgets", "تعديل الميزانيات", "Edit legacy budget records within authorized project or portfolio scope.", "تعديل سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["Budget.View"], true),
        new(
            "b0000247-0000-0000-0000-000000000247", "Budget.Delete", "Budget", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budgets", "Budgets", "الميزانيات", PermissionLevel.Action,
            "Delete budgets", "حذف الميزانيات", "Delete legacy budget records within authorized project or portfolio scope.", "حذف سجلات الميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "Critical", true, ["Budget.View"], true),
        new(
            "b0000248-0000-0000-0000-000000000248", "BudgetRevision.Create", "BudgetRevision", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Create budget revisions", "إنشاء مراجعات الميزانية", "Create a budget revision draft for a budget in the actor's authorized project or portfolio scope.", "إنشاء مسودة مراجعة ميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000249-0000-0000-0000-000000000249", "BudgetRevision.EditDraft", "BudgetRevision", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Edit budget revision drafts", "تعديل مسودات مراجعة الميزانية", "Edit a draft budget revision within the actor's authorized project or portfolio scope.", "تعديل مسودة مراجعة ميزانية ضمن نطاق المشروع أو المحفظة المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000250-0000-0000-0000-000000000250", "BudgetRevision.SubmitForReview", "BudgetRevision", "SubmitForReview",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Submit budget revisions for review", "إرسال مراجعات الميزانية للمراجعة", "Submit a draft budget revision into the review workflow within authorized scope.", "إرسال مسودة مراجعة الميزانية إلى مسار المراجعة ضمن النطاق المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000251-0000-0000-0000-000000000251", "BudgetRevision.Cancel", "BudgetRevision", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Cancel budget revisions", "إلغاء مراجعات الميزانية", "Cancel an eligible budget revision under workflow-state rules and authorized project scope.", "إلغاء مراجعة ميزانية مؤهلة وفق حالة سير العمل ونطاق المشروع المصرح به.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000252-0000-0000-0000-000000000252", "BudgetRevision.Delete", "BudgetRevision", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Delete budget revisions", "حذف مراجعات الميزانية", "Delete an eligible draft budget revision within authorized project scope.", "حذف مسودة مراجعة ميزانية مؤهلة ضمن نطاق المشروع المصرح به.",
            "Critical", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000253-0000-0000-0000-000000000253", "Cost.View", "Cost", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "costs", "Costs", "التكاليف", PermissionLevel.View,
            "View costs", "عرض التكاليف", "View cost entries within authorized project management scope.", "عرض قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000254-0000-0000-0000-000000000254", "Cost.Create", "Cost", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Create cost entries", "إنشاء قيود التكاليف", "Create cost entries within authorized project management scope.", "إنشاء قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000255-0000-0000-0000-000000000255", "Cost.Edit", "Cost", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Edit cost entries", "تعديل قيود التكاليف", "Edit cost entries within authorized project management scope.", "تعديل قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000256-0000-0000-0000-000000000256", "Cost.Delete", "Cost", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "costs", "Costs", "التكاليف", PermissionLevel.Action,
            "Delete cost entries", "حذف قيود التكاليف", "Delete cost entries within authorized project management scope.", "حذف قيود التكاليف ضمن نطاق إدارة المشروع المصرح به.",
            "Critical", true, ["Cost.View"], true, PermissionScope.Project),
        new(
            "b0000269-0000-0000-0000-000000000269", "CostUtilization.Export", "CostUtilization", "Export",
            "capacity", "Capacity", "السعة",
            "cost-utilization", "Cost & Utilization", "التكلفة والاستغلال", PermissionLevel.Action,
            "Export Cost & Utilization", "تصدير تقرير التكلفة والاستغلال", "Export Cost & Utilization data within the current Tenant and the user's authorized management scope. The export endpoint also requires Resource.View; resource cost-rate fields remain redacted unless Resource.ViewCostRate is effective.", "تصدير بيانات التكلفة والاستغلال ضمن المستأجر الحالي ونطاق الإدارة المصرح به للمستخدم. يتطلب مسار التصدير أيضًا صلاحية Resource.View، وتظل حقول معدلات تكلفة الموارد مخفية ما لم تكن صلاحية Resource.ViewCostRate فعالة.",
            "High", true, ["Resource.View"], true, PermissionScope.Tenant, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000257-0000-0000-0000-000000000257", "Commitment.View", "Commitment", "View",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.View,
            "View commitments", "عرض الالتزامات", "View purchase commitments within authorized project management scope.", "عرض الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000258-0000-0000-0000-000000000258", "Commitment.Create", "Commitment", "Create",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.Action,
            "Create commitments", "إنشاء الالتزامات", "Create purchase commitments within authorized project management scope.", "إنشاء الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000259-0000-0000-0000-000000000259", "Commitment.Edit", "Commitment", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.Action,
            "Edit commitments", "تعديل الالتزامات", "Edit eligible purchase commitments within authorized project management scope.", "تعديل الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000260-0000-0000-0000-000000000260", "Commitment.Close", "Commitment", "Close",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.Action,
            "Close commitments", "إغلاق الالتزامات", "Close purchase commitments within authorized project management scope.", "إغلاق الالتزامات الشرائية ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000261-0000-0000-0000-000000000261", "Commitment.Cancel", "Commitment", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.Action,
            "Cancel commitments", "إلغاء الالتزامات", "Cancel eligible purchase commitments within authorized project management scope.", "إلغاء الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "High", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000262-0000-0000-0000-000000000262", "Commitment.Delete", "Commitment", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "commitments", "Purchase commitments", "الالتزامات الشرائية", PermissionLevel.Action,
            "Delete commitments", "حذف الالتزامات", "Delete eligible purchase commitments within authorized project management scope.", "حذف الالتزامات الشرائية المؤهلة ضمن نطاق إدارة المشروع المصرح به.",
            "Critical", true, ["Commitment.View"], true, PermissionScope.Project),
        new(
            "b0000263-0000-0000-0000-000000000263", "PaymentObligation.View", "PaymentObligation", "View",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned payments", "المدفوعات المخططة", PermissionLevel.View,
            "View planned payment obligations", "عرض التزامات الدفع المخططة", "View payment obligations and their settlements within authorized project scope.", "عرض التزامات الدفع والتسويات ضمن نطاق المشروع المصرح به.",
            "High", true, [], true, PermissionScope.Project),
        new(
            "b0000264-0000-0000-0000-000000000264", "PaymentObligation.Create", "PaymentObligation", "Create",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Create payment obligations", "إنشاء التزامات الدفع", "Create a planned payment obligation within authorized project scope.", "إنشاء التزام دفع مخطط ضمن نطاق المشروع المصرح به.",
            "High", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000265-0000-0000-0000-000000000265", "PaymentObligation.Edit", "PaymentObligation", "Edit",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Edit payment obligations", "تعديل التزامات الدفع", "Edit an eligible payment obligation within authorized project scope.", "تعديل التزام دفع مؤهل ضمن نطاق المشروع المصرح به.",
            "High", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000266-0000-0000-0000-000000000266", "PaymentObligation.Delete", "PaymentObligation", "Delete",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Delete payment obligations", "حذف التزامات الدفع", "Delete an eligible payment obligation without settlements within authorized project scope.", "حذف التزام دفع مؤهل لا توجد عليه تسويات ضمن نطاق المشروع المصرح به.",
            "Critical", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000267-0000-0000-0000-000000000267", "PaymentObligationSettlement.Record", "PaymentObligationSettlement", "Record",
            "financial-management", "Financial Management", "الإدارة المالية", "planned-payments", "Planned payments", "المدفوعات المخططة", PermissionLevel.Action,
            "Record payment obligation settlement", "تسجيل تسوية التزام الدفع", "Record an actual settlement against a planned payment obligation within authorized tenant, project, and fiscal-year scope.", "تسجيل تسوية فعلية مقابل التزام دفع مخطط ضمن نطاق المستأجر والمشروع والسنة المالية المصرح به.",
            "Critical", true, ["PaymentObligation.View"], true, PermissionScope.Project),
        new(
            "b0000215-0000-0000-0000-000000000215", "BudgetRevision.View", "BudgetRevision", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.View,
            "View budget revisions", "عرض مراجعات الميزانية", "View budget revisions within authorized Tenant and Project management scope.", "عرض مراجعات الميزانية ضمن نطاق المستأجر والمشروع المصرح به.",
            "Low", false, [], true, PermissionScope.Project),
        new(
            "b0000216-0000-0000-0000-000000000216", "BudgetRevision.Approve", "BudgetRevision", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "budget-revisions", "Budget revisions", "مراجعات الميزانية", PermissionLevel.Action,
            "Approve or reject budget revisions", "اعتماد أو رفض مراجعات الميزانية", "Approve or reject a submitted budget revision. The requester cannot decide their own revision.", "اعتماد أو رفض مراجعة ميزانية مقدمة. لا يجوز لمقدم الطلب البت في طلبه.",
            "High", true, ["BudgetRevision.View"], true, PermissionScope.Project),
        new(
            "b0000167-0000-0000-0000-000000000167", "Governance.AssignmentRecovery.Initiate", "Governance.AssignmentRecovery", "Initiate",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment recovery", "استعادة الإسنادات", PermissionLevel.Action,
            "Governance assignment recovery · Initiate", "استعادة تعيينات الحوكمة · بدء", "Initiate initial manager assignments through the existing approval workflow", "تنفيذ إجراء استعادة تعيينات الحوكمة · بدء على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Governance.AssignmentRecovery.View"], true, IsTenantAdminBaseline: true),
        new(
            "b0000168-0000-0000-0000-000000000168", "Governance.AssignmentRecovery.Manage", "Governance.AssignmentRecovery", "Manage",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment recovery", "استعادة الإسنادات", PermissionLevel.Action,
            "Execute assignment recovery", "تنفيذ استعادة الإسناد", "Reconcile governance references, assign project sponsors, and initiate administrative handovers", "تنفيذ إجراء تنفيذ استعادة الإسناد على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Governance.AssignmentRecovery.View"], true, IsTenantAdminBaseline: true),
        new(
            "b0000166-0000-0000-0000-000000000166", "Governance.AssignmentRecovery.View", "Governance.AssignmentRecovery", "View",
            "governance", "Governance", "الحوكمة",
            "assignment-recovery", "Assignment recovery", "استعادة الإسنادات", PermissionLevel.View,
            "Governance assignment recovery · View", "استعادة تعيينات الحوكمة · عرض", "Discover tenant entities with missing governance assignments", "تنفيذ إجراء استعادة تعيينات الحوكمة · عرض على Assignment recovery مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true, IsTenantAdminBaseline: true),
        new(
            "b0000149-0000-0000-0000-000000000149", "Handover.AdminInitiate", "Handover", "AdminInitiate",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "handovers", "Handovers", "تسليم الإدارة", PermissionLevel.Action,
            "Handovers · Initiate handovers administratively", "التسليمات · بدء التسليمات إدارياً", "Initiate administrative handovers and initial manager assignments", "تنفيذ إجراء التسليمات · بدء التسليمات إدارياً على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Handover.View"], true),
        new(
            "b0000110-0000-0000-0000-000000000110", "Handover.Approve", "Handover", "Approve",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "handovers", "Handovers", "تسليم الإدارة", PermissionLevel.Action,
            "Handover · Accept or reject", "تسليم المسؤولية · قبول أو رفض", "Accept or reject project and manager handovers", "تنفيذ إجراء تسليم المسؤولية · قبول أو رفض على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Handover.View"], true),
        new(
            "b0000109-0000-0000-0000-000000000109", "Handover.Manage", "Handover", "Manage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "handovers", "Handovers", "تسليم الإدارة", PermissionLevel.Action,
            "Request or Cancel Handovers", "طلب أو إلغاء عمليات التسليم", "Request or cancel project and manager handovers", "تنفيذ إجراء التسليمات · إدارة على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Handover.View"], true),
        new(
            "b0000108-0000-0000-0000-000000000108", "Handover.View", "Handover", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "handovers", "Handovers", "تسليم الإدارة", PermissionLevel.View,
            "Handovers · View", "التسليمات · عرض", "View project and manager handovers", "تنفيذ إجراء التسليمات · عرض على Handovers مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000128-0000-0000-0000-000000000128", "Meeting.Create", "Meeting", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Meetings · Create", "الاجتماعات · إنشاء", "Create meetings", "تنفيذ إجراء الاجتماعات · إنشاء على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000131-0000-0000-0000-000000000131", "Meeting.Delete", "Meeting", "Delete",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Meetings · Delete", "الاجتماعات · حذف", "Delete meetings", "تنفيذ إجراء الاجتماعات · حذف على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Meeting.View"], true),
        new(
            "b0000129-0000-0000-0000-000000000129", "Meeting.Edit", "Meeting", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Meetings · Edit", "الاجتماعات · تعديل", "Edit meetings and attendees", "تنفيذ إجراء الاجتماعات · تعديل على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000130-0000-0000-0000-000000000130", "Meeting.Manage", "Meeting", "Manage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.Action,
            "Manage meeting minutes, comments, and work item links", "إدارة محاضر الاجتماعات والتعليقات وروابط عناصر العمل", "Manage minutes, comments, and work item links", "تنفيذ إجراء إدارة محاضر الاجتماعات والتعليقات وروابط عناصر العمل على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Meeting.View"], true),
        new(
            "b0000127-0000-0000-0000-000000000127", "Meeting.View", "Meeting", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "meetings", "Meetings", "الاجتماعات", PermissionLevel.View,
            "Meetings · View", "الاجتماعات · عرض", "View meetings and meeting minutes", "تنفيذ إجراء الاجتماعات · عرض على Meetings مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000171-0000-0000-0000-000000000171", "PaymentSettlement.Record", "PaymentSettlement", "Record",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Record payment settlement", "تسجيل تسوية دفعة", "Record actual project payment settlements within the actor's authorized project scope", "تنفيذ إجراء تسجيل تسوية دفعة على Payment Requests مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000118-0000-0000-0000-000000000118", "Planning.Lifecycle", "Planning", "Lifecycle",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "planning", "Planning", "التخطيط", PermissionLevel.Action,
            "Planning · Manage lifecycle", "التخطيط · إدارة دورة الحياة", "Manage planning working and baseline lifecycle", "تنفيذ إجراء التخطيط · إدارة دورة الحياة على Planning مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Planning.View"], true),
        new(
            "b0000116-0000-0000-0000-000000000116", "Planning.View", "Planning", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "planning", "Planning", "التخطيط", PermissionLevel.View,
            "Planning · View", "التخطيط · عرض", "View project planning data", "تنفيذ إجراء التخطيط · عرض على Planning مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000119-0000-0000-0000-000000000119", "Portfolio.View", "Portfolio", "View",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.View,
            "Portfolios · View", "المحافظ · عرض", "View portfolios", "عرض المحافظ الواقعة ضمن نطاق الإدارة المصرح للمستخدم.",
            "Low", false, [], true),
        new(
            "b0000121-0000-0000-0000-000000000121", "Program.View", "Program", "View",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.View,
            "Programs · View", "البرامج · عرض", "View programs", "عرض البرامج الواقعة ضمن نطاق الإدارة المصرح للمستخدم.",
            "Low", false, [], true),
        new(
            "b0000102-0000-0000-0000-000000000102", "Project.Create", "Project", "Create",
            "project-management", "Project Management", "إدارة المشاريع",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Projects · Create", "المشروعات · إنشاء", "Create projects", "تنفيذ إجراء المشروعات · إنشاء على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Project.View"], true),
        new(
            "b0000104-0000-0000-0000-000000000104", "Project.Delete", "Project", "Delete",
            "project-management", "Project Management", "إدارة المشاريع",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Projects · Delete", "المشروعات · حذف", "Delete projects", "تنفيذ إجراء المشروعات · حذف على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Project.View"], true),
        new(
            "b0000103-0000-0000-0000-000000000103", "Project.Edit", "Project", "Edit",
            "project-management", "Project Management", "إدارة المشاريع",
            "projects", "Projects", "المشروعات", PermissionLevel.Action,
            "Projects · Edit", "المشروعات · تعديل", "Edit project data", "تنفيذ إجراء المشروعات · تعديل على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Project.View"], true),
        new(
            "b0000101-0000-0000-0000-000000000101", "Project.View", "Project", "View",
            "project-management", "Project Management", "إدارة المشاريع",
            "projects", "Projects", "المشروعات", PermissionLevel.View,
            "Projects · View", "المشروعات · عرض", "View projects", "تنفيذ إجراء المشروعات · عرض على Projects مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000170-0000-0000-0000-000000000170", "ProjectAnnualBudget.Approve", "ProjectAnnualBudget", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Review annual budget: approve or return for changes", "مراجعة الميزانية السنوية: اعتماد أو إعادة للتعديل", "Decide an assigned budget review by approving or returning for changes; retain scope and separation-of-duties checks.", "اتخاذ قرار في مراجعة مُسندة بالاعتماد أو الإعادة للتعديل، مع الحفاظ على فحوص النطاق والفصل بين المهام.",
            "High", true, [], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000176-0000-0000-0000-000000000176", "ProjectAnnualBudget.CarryForward", "ProjectAnnualBudget", "CarryForward",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Authorize annual budget carry-forward", "اعتماد ترحيل رصيد الميزانية السنوية", "Authorize explicit free-balance and outstanding-commitment carry forward during Project year-end closing", "تفويض ترحيل الرصيد الحر والالتزامات المحجوزة بموافقة صريحة. لا تُمنح هذه الصلاحية لأي دور تلقائياً.",
            "High", true, ["ProjectAnnualBudgetClosing.View"], true),
        new(
            "b0000174-0000-0000-0000-000000000174", "ProjectAnnualBudget.Close", "ProjectAnnualBudget", "Close",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Prepare fiscal year closing", "إعداد إقفال السنة المالية", "Prepare, submit, and cancel Project fiscal-year closing statements within the actor's authorized project scope", "إعداد أو إرسال أو إلغاء إقفال السنة المالية للمشروع ضمن نطاق المشروع المصرح به.",
            "Medium", true, ["ProjectAnnualBudgetClosing.View"], true),
        new(
            "b0000175-0000-0000-0000-000000000175", "ProjectAnnualBudget.CloseApprove", "ProjectAnnualBudget", "CloseApprove",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.Action,
            "Approve or reject fiscal year closing", "اعتماد أو رفض إقفال السنة المالية", "Approve or reject an assigned Project fiscal-year closing statement", "اعتماد أو رفض إقفال سنة مالية معيّن للمشروع. لا يجوز لمُعد الإقفال اعتماده.",
            "High", true, ["ProjectAnnualBudgetClosing.View"], true),
        new(
            "b0000177-0000-0000-0000-000000000177", "ProjectApproval.Override", "ProjectApproval", "Override",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Override approval settings for one submission", "تجاوز إعدادات الاعتماد لإرسال واحد", "Apply a reasoned exception to one approval submission only; does not change persistent Project Settings.", "تطبيق استثناء مسبب على إرسال واحد فقط، دون تغيير إعدادات المشروع الدائمة.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000173-0000-0000-0000-000000000173", "ProjectPaymentReversal.Approve", "ProjectPaymentReversal", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Approve or reject payment reversal", "اعتماد أو رفض عكس دفعة", "Approve or reject project Payment Request commitment reversals assigned within the actor's authorized project scope", "اعتماد أو رفض طلب مُسند لتحرير التزام غير مدفوع. لا تمنح هذه الصلاحية حق تسجيل التسويات أو إصدار المبالغ المستردة.",
            "High", true, [], true),
        new(
            "b0000172-0000-0000-0000-000000000172", "ProjectPaymentReversal.Create", "ProjectPaymentReversal", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Create payment reversal request", "إنشاء طلب عكس دفعة", "Create and submit approved project Payment Request commitment reversals", "إنشاء وإرسال طلبات عكس الالتزام المالي غير المدفوع. لا تمنح هذه الصلاحية حق اعتماد طلبات العكس.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000237-0000-0000-0000-000000000237", "Report.View", "Report", "View",
            "reporting", "Reporting", "التقارير",
            "reports", "Reports", "التقارير", PermissionLevel.View,
            "View reports", "عرض التقارير", "View reports within verified tenant and organization query scope.", "عرض التقارير ضمن نطاق استعلام المستأجر والمؤسسة بعد التحقق منه.",
            "Low", false, [], true),
        new(
            "b0000017-0000-0000-0000-000000000017", "Resource.Assign", "Resource", "Assign",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Resources · Assign", "الموارد · إسناد", "Assign resources to projects", "تنفيذ إجراء الموارد · إسناد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000024-0000-0000-0000-000000000024", "Resource.Create", "Resource", "Create",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Resources · Create", "الموارد · إنشاء", "Create resources and vendors", "تنفيذ إجراء الموارد · إنشاء على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000026-0000-0000-0000-000000000026", "Resource.Delete", "Resource", "Delete",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Deactivate or delete resources and vendors", "تعطيل الموارد والموردين أو حذفهم", "Deactivate or delete resources and vendors", "تنفيذ إجراء تعطيل الموارد والموردين أو حذفهم على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Resource.View"], true),
        new(
            "b0000025-0000-0000-0000-000000000025", "Resource.Edit", "Resource", "Edit",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Resources · Edit", "الموارد · تعديل", "Edit resources and vendors", "تنفيذ إجراء الموارد · تعديل على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000022-0000-0000-0000-000000000022", "Resource.EditCostRate", "Resource", "EditCostRate",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "Edit resource cost rates", "تعديل تكاليف الموارد", "Edit resource cost rates", "تنفيذ إجراء تعديل تكاليف الموارد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "High", true, ["Resource.ViewCostRate", "Resource.View"], true),
        new(
            "b0000016-0000-0000-0000-000000000016", "Resource.View", "Resource", "View",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.View,
            "Resources · View", "الموارد · عرض", "View resources", "تنفيذ إجراء الموارد · عرض على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000021-0000-0000-0000-000000000021", "Resource.ViewCostRate", "Resource", "ViewCostRate",
            "capacity", "Capacity", "السعة",
            "resources", "Resources", "الموارد", PermissionLevel.Action,
            "View resource cost rates", "عرض تكاليف الموارد", "View resource cost rates", "تنفيذ إجراء عرض تكاليف الموارد على Resources مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Resource.View"], true),
        new(
            "b0000112-0000-0000-0000-000000000112", "Risk.Create", "Risk", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "risk-management", "Risk management", "إدارة المخاطر", PermissionLevel.Action,
            "Risks · Create", "المخاطر · إنشاء", "Create project risks", "تنفيذ إجراء المخاطر · إنشاء على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000114-0000-0000-0000-000000000114", "Risk.Delete", "Risk", "Delete",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "risk-management", "Risk management", "إدارة المخاطر", PermissionLevel.Action,
            "Risks · Delete", "المخاطر · حذف", "Delete risk children", "تنفيذ إجراء المخاطر · حذف على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Risk.View"], true),
        new(
            "b0000113-0000-0000-0000-000000000113", "Risk.Edit", "Risk", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "risk-management", "Risk management", "إدارة المخاطر", PermissionLevel.Action,
            "Risks · Edit", "المخاطر · تعديل", "Edit project risks and follow-through", "تنفيذ إجراء المخاطر · تعديل على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000115-0000-0000-0000-000000000115", "Risk.Mitigate", "Risk", "Mitigate",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "risk-management", "Risk management", "إدارة المخاطر", PermissionLevel.Action,
            "Risks · Mitigate", "المخاطر · تخفيف المخاطر", "Transition and mitigate project risks", "تنفيذ إجراء المخاطر · تخفيف المخاطر على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Risk.View"], true),
        new(
            "b0000111-0000-0000-0000-000000000111", "Risk.View", "Risk", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "risk-management", "Risk management", "إدارة المخاطر", PermissionLevel.View,
            "Risks · View", "المخاطر · عرض", "View project risks", "تنفيذ إجراء المخاطر · عرض على Risk management مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000238-0000-0000-0000-000000000238", "Settings.View", "Settings", "View",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant and organization settings", "إعدادات المستأجر والمؤسسة", PermissionLevel.View,
            "Settings · Read", "الإعدادات · قراءة", "View tenant and organization settings records within authorized tenant and management scope.", "عرض سجلات Tenant and organization settings ضمن نطاق المستأجر والإدارة المصرح به.",
            "Low", false, [], true),
        new(
            "b0000300-0000-0000-0000-000000000300", "EmailNotifications.View", "EmailNotifications", "View",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email notifications", "إشعارات البريد الإلكتروني", PermissionLevel.View,
            "Email notifications · View", "إشعارات البريد الإلكتروني · عرض", "View effective SMTP settings and scoped email delivery records within authorized Tenant and Organization scope.", "عرض إعدادات SMTP الفعالة وسجلات تسليم البريد ضمن نطاق المستأجر والمؤسسة المصرح به.",
            "Medium", false, ["Settings.View"], true, PermissionScope.Tenant),
        new(
            "b0000133-0000-0000-0000-000000000133", "Stakeholders.Manage", "Stakeholders", "Manage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-stakeholders", "Project stakeholders", "أصحاب المصلحة بالمشروع", PermissionLevel.Action,
            "Stakeholders · Manage", "أصحاب المصلحة · إدارة", "Manage project stakeholders", "تنفيذ إجراء أصحاب المصلحة · إدارة على Project stakeholders مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Stakeholders.Read"], true),
        new(
            "b0000132-0000-0000-0000-000000000132", "Stakeholders.Read", "Stakeholders", "Read",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-stakeholders", "Project stakeholders", "أصحاب المصلحة بالمشروع", PermissionLevel.View,
            "Stakeholders · Read", "أصحاب المصلحة · قراءة", "View project stakeholders", "تنفيذ إجراء أصحاب المصلحة · قراءة على Project stakeholders مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000158-0000-0000-0000-000000000158", "StatusReport.Create", "StatusReport", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Project status reports · Create", "تقارير حالة المشروع · إنشاء", "Create draft project status reports", "تنفيذ إجراء تقارير حالة المشروع · إنشاء على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000159-0000-0000-0000-000000000159", "StatusReport.Edit", "StatusReport", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Project status reports · Edit", "تقارير حالة المشروع · تعديل", "Edit draft project status reports", "تنفيذ إجراء تقارير حالة المشروع · تعديل على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000164-0000-0000-0000-000000000164", "StatusReport.Export", "StatusReport", "Export",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Project status reports · Export", "تقارير حالة المشروع · تصدير", "Export a project status report", "تنفيذ إجراء تقارير حالة المشروع · تصدير على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000165-0000-0000-0000-000000000165", "StatusReport.ManageConfiguration", "StatusReport", "ManageConfiguration",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Configure Project Reporting Scope", "إعداد نطاق التقارير الرسمية للمشروع", "Configure official project reporting scope", "تنفيذ إجراء إدارة إعدادات تقارير الحالة على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["StatusReport.View"], true),
        new(
            "b0000163-0000-0000-0000-000000000163", "StatusReport.Publish", "StatusReport", "Publish",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.Action,
            "Project status reports · Publish", "تقارير حالة المشروع · نشر", "Publish a Draft status report and capture its immutable project snapshot", "تنفيذ إجراء تقارير حالة المشروع · نشر على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["StatusReport.View"], true),
        new(
            "b0000157-0000-0000-0000-000000000157", "StatusReport.View", "StatusReport", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "project-status-reports", "Project status reports", "تقارير حالة المشروع", PermissionLevel.View,
            "Project status reports · View", "تقارير حالة المشروع · عرض", "View project status reports and performance", "تنفيذ إجراء تقارير حالة المشروع · عرض على Project status reports مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000031-0000-0000-0000-000000000031", "Team.Create", "Team", "Create",
            "capacity", "Capacity", "السعة",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Teams · Create", "الفرق · إنشاء", "Create teams", "تنفيذ إجراء الفرق · إنشاء على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Team.View"], true),
        new(
            "b0000033-0000-0000-0000-000000000033", "Team.Delete", "Team", "Delete",
            "capacity", "Capacity", "السعة",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Teams · Delete", "الفرق · حذف", "Deactivate or delete teams", "تنفيذ إجراء الفرق · حذف على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["Team.View"], true),
        new(
            "b0000032-0000-0000-0000-000000000032", "Team.Edit", "Team", "Edit",
            "capacity", "Capacity", "السعة",
            "teams", "Teams", "الفرق", PermissionLevel.Action,
            "Teams · Edit", "الفرق · تعديل", "Edit teams and manage their members", "تنفيذ إجراء الفرق · تعديل على Teams مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["Team.View"], true),
        new(
            "b0000030-0000-0000-0000-000000000030", "Team.View", "Team", "View",
            "capacity", "Capacity", "السعة",
            "teams", "Teams", "الفرق", PermissionLevel.View,
            "Teams · View", "الفرق · عرض", "View teams and their members", "عرض الفرق التابعة للمؤسسة وسجل عضويتها محدد الفترات. هذه الصلاحية وحدها لا تمنح إسناد الموارد أو الوصول إلى واجهات المشروعات.",
            "Low", false, [], true),
        new(
            "b0000143-0000-0000-0000-000000000143", "TimeTracking.ManageOwn", "TimeTracking", "ManageOwn",
            "capacity", "Capacity", "السعة",
            "time-tracking", "Time tracking", "تسجيل الوقت", PermissionLevel.Action,
            "Create, Edit, or Delete Own Time Entries", "إنشاء إدخالات الوقت الخاصة بي أو تعديلها أو حذفها", "Create, update, and delete own manual time entries", "تنفيذ إجراء إدارة سجلات الوقت الشخصية على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["TimeTracking.ViewOwn"], true),
        new(
            "b0000145-0000-0000-0000-000000000145", "TimeTracking.ManageTeam", "TimeTracking", "ManageTeam",
            "capacity", "Capacity", "السعة",
            "time-tracking", "Time tracking", "تسجيل الوقت", PermissionLevel.Action,
            "Manage team time entries", "إدارة سجلات وقت الفريق", "Manage team manual time entries within management scope", "تنفيذ إجراء إدارة سجلات وقت الفريق على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["TimeTracking.ViewTeam"], true),
        new(
            "b0000142-0000-0000-0000-000000000142", "TimeTracking.ViewOwn", "TimeTracking", "ViewOwn",
            "capacity", "Capacity", "السعة",
            "time-tracking", "Time tracking", "تسجيل الوقت", PermissionLevel.View,
            "View own time entries", "عرض سجلات الوقت الشخصية", "View own manual time entries", "تنفيذ إجراء عرض سجلات الوقت الشخصية على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000144-0000-0000-0000-000000000144", "TimeTracking.ViewTeam", "TimeTracking", "ViewTeam",
            "capacity", "Capacity", "السعة",
            "time-tracking", "Time tracking", "تسجيل الوقت", PermissionLevel.View,
            "View team time entries", "عرض سجلات وقت الفريق", "View team manual time entries within management scope", "تنفيذ إجراء عرض سجلات وقت الفريق على Time tracking مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000239-0000-0000-0000-000000000239", "Users.View", "Users", "View",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.View,
            "Users · Read", "المستخدمون · قراءة", "View users and access records within authorized tenant and management scope.", "عرض سجلات Users and access ضمن نطاق المستأجر والإدارة المصرح به.",
            "Low", false, [], true),
        new(
            "b0000240-0000-0000-0000-000000000240", "Users.ResetPassword", "Users", "ResetPassword",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.Action,
            "Reset user passwords", "إعادة تعيين كلمات مرور المستخدمين", "Generate a one-time temporary password for a local user", "تنفيذ إجراء إعادة تعيين كلمات مرور المستخدمين على Users and access مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", true, ["Users.View"], true),
        new(
            "b0000140-0000-0000-0000-000000000140", "Workflow.Manage", "Workflow", "Manage",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "workflow-administration", "Workflow administration", "إدارة سير العمل", PermissionLevel.Action,
            "Edit Workflow Drafts", "تعديل مسودات سير العمل", "Edit workflow drafts", "تنفيذ إجراء مسارات العمل · إدارة على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Workflow.View"], true, IsTenantAdminBaseline: true),
        new(
            "b0000141-0000-0000-0000-000000000141", "Workflow.Publish", "Workflow", "Publish",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "workflow-administration", "Workflow administration", "إدارة سير العمل", PermissionLevel.Action,
            "Workflows · Publish", "مسارات العمل · نشر", "Publish and retire workflow versions", "تنفيذ إجراء مسارات العمل · نشر على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", false, ["Workflow.View"], true, IsTenantAdminBaseline: true),
        new(
            "b0000139-0000-0000-0000-000000000139", "Workflow.View", "Workflow", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "workflow-administration", "Workflow administration", "إدارة سير العمل", PermissionLevel.View,
            "Workflows · View", "مسارات العمل · عرض", "View workflow definitions and versions", "تنفيذ إجراء مسارات العمل · عرض على Workflow administration مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true, IsTenantAdminBaseline: true),
        new(
            "b0000124-0000-0000-0000-000000000124", "WorkItem.Create", "WorkItem", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "work-items", "Work items", "عناصر العمل", PermissionLevel.Action,
            "Work items · Create", "عناصر العمل · إنشاء", "Create task and issue work items", "تنفيذ إجراء عناصر العمل · إنشاء على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["WorkItem.View"], true),
        new(
            "b0000126-0000-0000-0000-000000000126", "WorkItem.Delete", "WorkItem", "Delete",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "work-items", "Work items", "عناصر العمل", PermissionLevel.Action,
            "Work items · Delete", "عناصر العمل · حذف", "Delete task and issue work items", "تنفيذ إجراء عناصر العمل · حذف على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Medium", true, ["WorkItem.View"], true),
        new(
            "b0000125-0000-0000-0000-000000000125", "WorkItem.Edit", "WorkItem", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "work-items", "Work items", "عناصر العمل", PermissionLevel.Action,
            "Work items · Edit", "عناصر العمل · تعديل", "Edit task and issue work items", "تنفيذ إجراء عناصر العمل · تعديل على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, ["WorkItem.View"], true),
        new(
            "b0000123-0000-0000-0000-000000000123", "WorkItem.View", "WorkItem", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "work-items", "Work items", "عناصر العمل", PermissionLevel.View,
            "Work items · View", "عناصر العمل · عرض", "View task and issue work items", "تنفيذ إجراء عناصر العمل · عرض على Work items مع الالتزام بنطاق المستأجر والإدارة.",
            "Low", false, [], true),
        new(
            "b0000178-0000-0000-0000-000000000178", "ProjectAnnualBudget.View", "ProjectAnnualBudget", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.View,
            "View Annual Budgets", "عرض الميزانيات السنوية", "Open the annual-budget workspace within tenant and project scope. Review-only users see assigned records only.", "فتح مساحة الميزانيات السنوية ضمن نطاق المستأجر والمشروع. يقتصر عرض المراجعين على السجلات المُسندة إليهم.",
            "High", true, [], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000179-0000-0000-0000-000000000179", "ProjectPaymentRequest.View", "ProjectPaymentRequest", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.View,
            "View Payment Requests", "عرض طلبات الدفع", "View requests and approval history in tenant/project scope; reviewer-only lists are assignment-filtered.", "عرض الطلبات وسجل اعتمادها ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد.",
            "High", true, [], true),
        new(
            "b0000180-0000-0000-0000-000000000180", "ProjectAnnualBudgetClosing.View", "ProjectAnnualBudgetClosing", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "fiscal-year-closing", "Fiscal Year Closing", "إقفال السنة المالية", PermissionLevel.View,
            "View Fiscal Year Closing", "عرض إقفالات السنة المالية", "View closings in tenant/project scope; reviewer-only lists are assignment-filtered.", "عرض الإقفالات ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد.",
            "High", true, [], true),
        new(
            "b0000233-0000-0000-0000-000000000233", "ProjectSettlement.View", "ProjectSettlement", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "settlements-and-reversals", "Settlements & Reversals", "التسويات والعكوس", PermissionLevel.View,
            "View Settlements & Reversals", "عرض التسويات والعكوس", "View Project settlement, reversal, and planned-payment records within authorized Project scope. Assigned reviewers retain access only to their assigned workflow record.", "عرض سجلات تسويات المشروع وطلبات العكس والمدفوعات المخططة ضمن نطاق المشروع المصرح به. يقتصر وصول المراجعين على سجل الإجراء المسند إليهم.",
            "High", true, [], true),
        new(
            "b0000235-0000-0000-0000-000000000235", "ProjectFinancialAuditTrail.View", "ProjectFinancialAuditTrail", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "financial-audit-trail", "Financial Audit Trail", "سجل التدقيق المالي", PermissionLevel.View,
            "View Financial Audit Trail", "عرض سجل التدقيق المالي", "View immutable financial audit records within authorized Project scope. This permission never allows changing or deleting audit history.", "عرض سجلات التدقيق المالي غير القابلة للتعديل ضمن نطاق المشروع المصرح به. لا تسمح هذه الصلاحية بتعديل سجل التدقيق أو حذفه.",
            "High", true, [], true),
        new(
            "b0000268-0000-0000-0000-000000000268", "ProjectFinancialFoundation.View", "ProjectFinancialFoundation", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "financial-overview", "Financial Overview", "نظرة عامة مالية", PermissionLevel.View,
            "View Project Financial Overview", "عرض النظرة العامة المالية للمشروع", "Read the Project financial foundation summary, including the lifetime ceiling and source-backed actual-paid and outstanding-obligation totals when records are comparable. Legacy approved-payment allocation and available-budget fields may be null. Does not grant create, edit, approve, or export capabilities.", "قراءة ملخص الأساس المالي للمشروع، بما في ذلك السقف المالي مدى الحياة وإجماليات المدفوعات الفعلية والالتزامات المستحقة المدعومة بسجلات المصدر عندما تكون السجلات قابلة للمقارنة. قد لا تتوفر حقول تخصيص مدفوعات الطلبات المعتمدة والميزانية المتاحة. لا يمنح هذا الإذن صلاحيات الإنشاء أو التعديل أو الاعتماد أو التصدير.",
            "High", true, [], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000270-0000-0000-0000-000000000270", "ProgramFinancialRollup.View", "ProgramFinancialRollup", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "program-financial-rollup", "Program Financial Rollups", "التجميعات المالية للبرامج", PermissionLevel.View,
            "View Program Financial Rollups", "عرض التجميعات المالية للبرامج", "Read a Program financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Program/descendant Project management scope. Does not grant Project workflow actions or unscoped Project access.", "عرض التجميع المالي للبرنامج وتفاصيل المشاريع الموزعة على الصفحات، مع الاقتصار على المشاريع ضمن المستأجر الحالي ونطاق الإدارة المصرح به للبرنامج والمشاريع التابعة له. لا تمنح هذه الصلاحية إجراءات سير العمل المالية للمشروع أو الوصول إلى مشاريع خارج النطاق.",
            "High", true, [], true, PermissionScope.Program, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000271-0000-0000-0000-000000000271", "PortfolioFinancialRollup.View", "PortfolioFinancialRollup", "View",
            "financial-management", "Financial Management", "الإدارة المالية",
            "portfolio-financial-rollup", "Portfolio Financial Rollups", "التجميعات المالية للمحافظ", PermissionLevel.View,
            "View Portfolio Financial Rollups", "عرض التجميعات المالية للمحافظ", "Read a Portfolio financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Portfolio/descendant Program and Project management scope. Does not grant Project workflow actions or unscoped Project access.", "عرض التجميع المالي للمحفظة وتفاصيل المشاريع الموزعة على الصفحات، مع الاقتصار على المشاريع ضمن المستأجر الحالي ونطاق الإدارة المصرح به للمحفظة والبرامج والمشاريع التابعة لها. لا تمنح هذه الصلاحية إجراءات سير العمل المالية للمشروع أو الوصول إلى مشاريع خارج النطاق.",
            "High", true, [], true, PermissionScope.Portfolio, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000181-0000-0000-0000-000000000181", "Dashboard.View", "Dashboard", "View",
            "reporting", "Reporting", "التقارير",
            "home-dashboards", "Home / Dashboards", "الرئيسية / لوحات المعلومات", PermissionLevel.View,
            "View Dashboards", "عرض لوحات المعلومات", "Open the dashboard workspace. Each widget data API continues to enforce its own permission and tenant/organization scope.", "فتح مساحة لوحات المعلومات، مع استمرار كل واجهة بيانات لعنصر لوحة بعينه في تطبيق صلاحياتها ونطاق المستأجر والمؤسسة.",
            "Medium", false, [], true),
        new(
            "b0000182-0000-0000-0000-000000000182", "ProjectAnnualBudget.Create", "ProjectAnnualBudget", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Create Annual Budget", "إنشاء ميزانية سنوية", "Create an annual-budget draft within project and fiscal-year scope.", "إنشاء مسودة ميزانية سنوية ضمن نطاق المشروع والسنة المالية.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000183-0000-0000-0000-000000000183", "ProjectAnnualBudget.EditDraft", "ProjectAnnualBudget", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Edit Draft Budget", "تعديل مسودة الميزانية", "Edit an eligible draft before submission.", "تعديل مسودة مستوفية للشروط قبل إرسالها.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000184-0000-0000-0000-000000000184", "ProjectAnnualBudget.SubmitForReview", "ProjectAnnualBudget", "SubmitForReview",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Submit for Review", "إرسال للمراجعة", "Submit an eligible draft to its configured review workflow.", "إرسال مسودة مستوفية للشروط إلى مسار المراجعة المحدد.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000185-0000-0000-0000-000000000185", "ProjectAnnualBudget.ReviseApproved", "ProjectAnnualBudget", "ReviseApproved",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Revise Approved Budget", "تعديل الميزانية المعتمدة", "Submit an eligible revision to an approved budget.", "إرسال تعديل مستوفٍ للشروط على ميزانية معتمدة.",
            "High", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000186-0000-0000-0000-000000000186", "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget", "ManageApprovalConfiguration",
            "financial-management", "Financial Management", "الإدارة المالية",
            "annual-budgets", "Annual Budgets", "الموازنات السنوية", PermissionLevel.Action,
            "Change Annual Budget Approval Defaults", "تغيير الإعدادات الافتراضية لاعتماد الميزانية السنوية", "Change persistent Project Settings approval defaults; distinct from a one-submission override.", "تغيير إعدادات الاعتماد الافتراضية الدائمة للمشروع، بمعزل عن تجاوز إرسال واحد.",
            "Critical", true, ["ProjectAnnualBudget.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000187-0000-0000-0000-000000000187", "ProjectPaymentRequest.Create", "ProjectPaymentRequest", "Create",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Create Payment Request", "إنشاء طلب دفع", "Create a request draft within project scope and currency rules.", "إنشاء مسودة طلب ضمن نطاق المشروع وقواعد العملة.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000188-0000-0000-0000-000000000188", "ProjectPaymentRequest.EditDraft", "ProjectPaymentRequest", "EditDraft",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Edit Draft Payment Request", "تعديل مسودة طلب دفع", "Edit an eligible request draft before submission.", "تعديل مسودة طلب مستوفية للشروط قبل إرسالها.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000189-0000-0000-0000-000000000189", "ProjectPaymentRequest.Submit", "ProjectPaymentRequest", "Submit",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Submit Payment Request", "إرسال طلب دفع", "Submit an eligible request to its approval workflow.", "إرسال طلب مستوفٍ للشروط إلى مسار الاعتماد.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000190-0000-0000-0000-000000000190", "ProjectPaymentRequest.Cancel", "ProjectPaymentRequest", "Cancel",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Cancel Payment Request", "إلغاء طلب دفع", "Cancel an eligible request under workflow-state rules; distinct from reversing settlement.", "إلغاء طلب مستوفٍ للشروط وفق حالته في سير العمل؛ وهو مستقل عن عكس التسوية.",
            "High", true, ["ProjectPaymentRequest.View"], true),
        new(
            "b0000191-0000-0000-0000-000000000191", "ProjectPaymentRequest.Approve", "ProjectPaymentRequest", "Approve",
            "financial-management", "Financial Management", "الإدارة المالية",
            "payment-requests", "Payment Requests", "طلبات الدفع", PermissionLevel.Action,
            "Approve or Reject Payment Request", "اعتماد أو رفض طلب دفع", "Decide only an assigned request; active team membership, project scope, exact reviewer assignment, and separation of duties remain required.", "اتخاذ قرار في طلب مُسند فقط؛ مع استمرار اشتراط عضوية الفريق ونطاق المشروع والإسناد الدقيق والفصل بين المهام.",
            "Critical", true, [], true),
        new(
            "b0000192-0000-0000-0000-000000000192", "Program.CreateAcrossPortfolios", "Program", "CreateAcrossPortfolios",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Create Programs Across Portfolios", "إنشاء برامج عبر المحافظ", "Allow creation outside native Portfolio assignment only within eligible tenant and organization scope; still requires Program.Create.", "السماح بالإنشاء خارج إسناد المحفظة الأصلي داخل نطاق المستأجر والمؤسسة المؤهل، مع استمرار اشتراط Program.Create.",
            "Critical", true, ["Program.View", "Portfolio.View", "Program.Create"], true, PermissionScope.Organization),
        new(
            "b0000193-0000-0000-0000-000000000193", "Users.ManageAccounts", "Users", "ManageAccounts",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.Action,
            "Create Users and Update Profiles", "إنشاء المستخدمين وتحديث ملفاتهم الشخصية", "Create users and update profile details. Account activation/deactivation requires Users.SetStatus. Excludes role assignment, permission overrides, and password reset.", "إنشاء حسابات المستخدمين وتعديل بياناتهم. يتطلب تفعيل الحساب أو تعطيله صلاحية Users.SetStatus. لا تشمل هذه الصلاحية إسناد الأدوار أو تجاوزات الصلاحيات أو إعادة تعيين كلمة المرور.",
            "High", true, ["Users.View"], true),
        new(
            "b0000217-0000-0000-0000-000000000217", "Users.SetStatus", "Users", "SetStatus",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.Action,
            "Activate or Deactivate User Accounts", "تفعيل حسابات المستخدمين أو تعطيلها", "Activate or deactivate a tenant user account. This does not grant profile editing, role assignment, direct permission overrides, or password reset.", "تفعيل حساب مستخدم في المستأجر أو تعطيله. لا تمنح هذه الصلاحية تعديل بيانات الحساب أو إسناد الأدوار أو تجاوزات الصلاحيات المباشرة أو إعادة تعيين كلمة المرور.",
            "High", true, ["Users.View"], true),
        new(
            "b0000195-0000-0000-0000-000000000195", "Users.AssignRoles", "Users", "AssignRoles",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.Action,
            "Assign Roles to Users", "إسناد الأدوار للمستخدمين", "Assign/remove tenant roles; changes effective access and require audit.", "إسناد أدوار المستأجر أو إزالتها؛ وهي تغييرات تؤثر على الوصول الفعلي وتتطلب التدقيق.",
            "Critical", true, ["Users.View"], true),
        new(
            "b0000196-0000-0000-0000-000000000196", "UserPermissions.Manage", "UserPermissions", "Manage",
            "administration", "Administration", "الإدارة",
            "users-and-access", "Users and access", "المستخدمون والوصول", PermissionLevel.Action,
            "Add or Remove Direct Permission Overrides", "إضافة تجاوزات الصلاحيات المباشرة أو إزالتها", "Add/remove per-user Allow/Deny overrides with audit and delegation limits.", "إضافة أو إزالة تجاوزات السماح أو المنع للمستخدم مع التدقيق وحدود التفويض.",
            "Critical", true, ["Users.View"], true),
        new(
            "b0000197-0000-0000-0000-000000000197", "Settings.ManageTenantConfiguration", "Settings", "ManageTenantConfiguration",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant and organization settings", "إعدادات المستأجر والمؤسسة", PermissionLevel.Action,
            "Update Tenant Settings", "تحديث إعدادات المستأجر", "Update tenant settings permitted by typed definitions; platform settings remain separate.", "تحديث إعدادات المستأجر المسموح بها وفق التعريفات؛ وتظل إعدادات المنصة منفصلة.",
            "High", true, ["Settings.View"], true),
        new(
            "b0000301-0000-0000-0000-000000000301", "EmailNotifications.Manage", "EmailNotifications", "Manage",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email notifications", "إشعارات البريد الإلكتروني", PermissionLevel.Action,
            "Email notifications · Manage settings", "إشعارات البريد الإلكتروني · إدارة الإعدادات", "Create, update, and reset scoped SMTP profiles. Credentials remain protected and are never returned.", "إنشاء ملفات SMTP وتعديلها وإعادة ضبطها ضمن النطاق المصرح به. تظل بيانات الاعتماد محمية ولا يتم إرجاعها.",
            "Critical", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000302-0000-0000-0000-000000000302", "EmailNotifications.Test", "EmailNotifications", "Test",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email notifications", "إشعارات البريد الإلكتروني", PermissionLevel.Action,
            "Email notifications · Test", "إشعارات البريد الإلكتروني · اختبار", "Test the scoped SMTP connection or send a test only to the authenticated actor's active account address.", "اختبار اتصال SMTP ضمن النطاق أو إرسال رسالة اختبار إلى عنوان الحساب النشط للمستخدم الحالي فقط.",
            "High", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000303-0000-0000-0000-000000000303", "EmailNotifications.Logs", "EmailNotifications", "Logs",
            "administration", "Administration", "الإدارة",
            "email-notifications", "Email notifications", "إشعارات البريد الإلكتروني", PermissionLevel.View,
            "Email notifications · View delivery logs", "إشعارات البريد الإلكتروني · عرض سجلات التسليم", "View scoped email event, attempt, and provider-acceptance status without message bodies or credentials.", "عرض أحداث البريد ومحاولاته وحالة قبول الخادم ضمن النطاق، دون محتوى الرسائل أو بيانات الاعتماد.",
            "Medium", true, ["EmailNotifications.View"], true, PermissionScope.Tenant),
        new(
            "b0000199-0000-0000-0000-000000000199", "Program.Create", "Program", "Create",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Create Program", "إنشاء برنامج", "Create a Program under an eligible Portfolio. Native assignment is required unless the explicit cross-Portfolio capability is also granted.", "إنشاء برنامج تحت محفظة مؤهلة. يلزم إسناد المحفظة ما لم تُمنح صلاحية الإنشاء عبر المحافظ صراحةً.",
            "High", true, ["Program.View"], true),
        new(
            "b0000200-0000-0000-0000-000000000200", "Program.Edit", "Program", "Edit",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Edit Program", "تعديل برنامج", "Edit an existing Program within native management scope.", "تعديل برنامج قائم ضمن نطاق الإدارة الأصلي.",
            "High", true, ["Program.View"], true),
        new(
            "b0000201-0000-0000-0000-000000000201", "Program.Archive", "Program", "Archive",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Archive Program", "أرشفة برنامج", "Archive a Program within native management scope.", "أرشفة برنامج ضمن نطاق الإدارة الأصلي.",
            "High", true, ["Program.View"], true),
        new(
            "b0000202-0000-0000-0000-000000000202", "Program.Reactivate", "Program", "Reactivate",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "programs", "Programs", "البرامج", PermissionLevel.Action,
            "Reactivate Program", "إعادة تفعيل برنامج", "Reactivate an archived Program within native management scope.", "إعادة تفعيل برنامج مؤرشف ضمن نطاق الإدارة الأصلي.",
            "Medium", true, ["Program.View"], true),
        new(
            "b0000203-0000-0000-0000-000000000203", "Portfolio.Create", "Portfolio", "Create",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Create Portfolio", "إنشاء محفظة", "Create a Portfolio within tenant and organization authority.", "إنشاء محفظة ضمن صلاحيات المستأجر والمؤسسة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000204-0000-0000-0000-000000000204", "Portfolio.Edit", "Portfolio", "Edit",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Edit Portfolio", "تعديل محفظة", "Edit a Portfolio within tenant and organization authority.", "تعديل محفظة ضمن صلاحيات المستأجر والمؤسسة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000205-0000-0000-0000-000000000205", "Portfolio.Archive", "Portfolio", "Archive",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Archive Portfolio", "أرشفة محفظة", "Archive a Portfolio subject to tenant, organization, and lifecycle checks.", "أرشفة محفظة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة.",
            "High", true, ["Portfolio.View"], true),
        new(
            "b0000206-0000-0000-0000-000000000206", "Portfolio.Reactivate", "Portfolio", "Reactivate",
            "portfolio-program-management", "Portfolio & Program Management", "إدارة المحافظ والبرامج",
            "portfolios", "Portfolios", "المحافظ", PermissionLevel.Action,
            "Reactivate Portfolio", "إعادة تفعيل محفظة", "Reactivate an archived Portfolio subject to tenant, organization, and lifecycle checks.", "إعادة تفعيل محفظة مؤرشفة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة.",
            "Medium", true, ["Portfolio.View"], true),
        new(
            "b0000207-0000-0000-0000-000000000207", "ProjectFinancialCeiling.ApproveChange", "ProjectFinancialCeiling", "ApproveChange",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "change-requests", "Change requests", "طلبات التغيير", PermissionLevel.Action,
            "Approve Project Ceiling Change", "اعتماد تعديل سقف المشروع", "Qualify approval of a change request that alters the approved lifetime ceiling. Also requires the normal ChangeRequest.Approve decision capability, exact workflow assignment, project scope, and separation of duties.", "تقييد اعتماد طلب تغيير يبدل السقف المالي المعتمد طوال عمر المشروع. يتطلب أيضًا صلاحية قرار ChangeRequest.Approve والإسناد الدقيق في سير العمل ونطاق المشروع والفصل بين المهام.",
            "Critical", true, ["ChangeRequest.Approve"], true),
        new(
            "b0000212-0000-0000-0000-000000000212", "Roles.View", "Roles", "View",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles and permissions", "الأدوار والصلاحيات", PermissionLevel.View,
            "View Roles and Permissions", "عرض الأدوار والصلاحيات", "Open the tenant role and permission workspace. Role data remains tenant-scoped and delegation-limited.", "فتح مساحة الأدوار والصلاحيات الخاصة بالمستأجر مع الالتزام بنطاق المستأجر وحدود تفويض الصلاحيات.",
            "Critical", true, [], true),
        new(
            "b0000213-0000-0000-0000-000000000213", "ReferenceData.View", "ReferenceData", "View",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference data", "البيانات المرجعية", PermissionLevel.View,
            "View Reference Data", "عرض البيانات المرجعية", "Open tenant-scoped reference data and lookup workspaces.", "فتح مساحات البيانات المرجعية والقوائم المساندة ضمن نطاق المستأجر.",
            "Critical", true, [], true),
        new(
            "b0000225-0000-0000-0000-000000000225", "ReferenceData.Create", "ReferenceData", "Create",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference data", "البيانات المرجعية", PermissionLevel.Action,
            "Create Reference Data", "إنشاء بيانات مرجعية", "Create supported tenant-managed reference data, including currencies, vendor categories, work-item statuses, and Project Roles.", "إنشاء البيانات المرجعية التي يديرها المستأجر، بما في ذلك العملات وفئات الموردين وحالات عناصر العمل وأدوار المشروع.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000226-0000-0000-0000-000000000226", "ReferenceData.Edit", "ReferenceData", "Edit",
            "administration", "Administration", "الإدارة",
            "reference-data", "Reference data", "البيانات المرجعية", PermissionLevel.Action,
            "Edit Reference Data", "تعديل البيانات المرجعية", "Edit supported tenant-managed reference data while preserving category-specific immutability and usage rules.", "تعديل البيانات المرجعية التي يديرها المستأجر مع احترام قواعد عدم التغيير والاستخدام الخاصة بكل فئة.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000227-0000-0000-0000-000000000227", "ReferenceData.Delete", "ReferenceData", "Delete", "administration", "Administration", "الإدارة",
            "reference-data", "Reference data", "البيانات المرجعية", PermissionLevel.Action,
            "Delete or Deactivate Reference Data", "حذف أو إيقاف البيانات المرجعية", "Soft-delete currencies or deactivate supported lookup values where existing business rules permit. Referenced or immutable values remain protected.", "حذف العملات حذفًا منطقيًا أو إيقاف القيم المرجعية التي تدعمها القواعد الحالية. تظل القيم المستخدمة أو غير القابلة للتغيير محمية.",
            "High", true, ["ReferenceData.View"], true),
        new(
            "b0000214-0000-0000-0000-000000000214", "TenantAdministration.ManageScope", "TenantAdministration", "ManageScope",
            "administration", "Administration", "الإدارة",
            "tenant-and-organization-settings", "Tenant and organization settings", "إعدادات المستأجر والمؤسسة", PermissionLevel.Action,
            "Administer Tenant Scope", "إدارة نطاق المستأجر", "Grant tenant-wide administrative management scope within this tenant only. Does not grant platform or cross-tenant authority.", "منح نطاق الإدارة على مستوى هذا المستأجر فقط، دون صلاحيات على المنصة أو على مستأجرين آخرين.",
            "Critical", true, ["Settings.View"], true),

        new(
            "b0000208-0000-0000-0000-000000000208", "Roles.Create", "Roles", "Create",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles and permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Create Role", "إنشاء دور", "Create a tenant-scoped role. Assigning permissions also requires Roles.ConfigurePermissions and may not exceed the actor's delegated permissions.", "إنشاء دور ضمن المستأجر. يتطلب إسناد الصلاحيات أيضاً Roles.ConfigurePermissions، ولا يجوز أن يتجاوز الصلاحيات المفوضة للمستخدم.",
            "High", true, ["Roles.View"], true),
        new(
            "b0000209-0000-0000-0000-000000000209", "Roles.EditDetails", "Roles", "EditDetails",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles and permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Edit Role Details", "تعديل تفاصيل الدور", "Change a role's name, description, and category without changing its permission grants.", "تعديل اسم الدور ووصفه وفئته دون تغيير الصلاحيات الممنوحة له.",
            "Medium", false, ["Roles.View"], true),
        new(
            "b0000210-0000-0000-0000-000000000210", "Roles.ConfigurePermissions", "Roles", "ConfigurePermissions",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles and permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Configure Role Permissions", "تكوين صلاحيات الدور", "Assign or remove permissions within the actor's effective delegation authority; never permits self-escalation.", "إسناد الصلاحيات أو إزالتها ضمن حدود التفويض الفعلي للمستخدم، دون السماح برفع صلاحياته بنفسه.",
            "Critical", true, ["Roles.View"], true),
        new(
            "b0000211-0000-0000-0000-000000000211", "Roles.Delete", "Roles", "Delete",
            "administration", "Administration", "الإدارة",
            "roles-and-permissions", "Roles and permissions", "الأدوار والصلاحيات", PermissionLevel.Action,
            "Delete Role", "حذف دور", "Delete an unused tenant role. System roles and roles assigned to users cannot be deleted.", "حذف دور غير مستخدم ضمن المستأجر. لا يمكن حذف أدوار النظام أو الأدوار المسندة إلى مستخدمين.",
            "High", true, ["Roles.View"], true),
        new(
            "b0000241-0000-0000-0000-000000000241", "Issue.View", "Issue", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "issues", "Issues", "المشكلات", PermissionLevel.View,
            "View Issues", "عرض المشكلات", "Read tenant issues within the user's assigned project scope.", "عرض مشكلات المستأجر ضمن نطاق المشاريع المسندة للمستخدم.",
            "Low", false, [], true, PermissionScope.Tenant),
        new(
            "b0000242-0000-0000-0000-000000000242", "Plugin.View", "Plugin", "View",
            "administration", "Administration", "الإدارة",
            "plugins", "Plugins", "الإضافات", PermissionLevel.View,
            "View tenant plugins", "عرض إضافات المستأجر", "Read the current tenant's plugin registry and activation metadata.", "عرض سجل إضافات المستأجر وبيانات التفعيل.",
            "Low", false, [], true, PermissionScope.Tenant),
        new(
            "b0000236-0000-0000-0000-000000000236", "Directory.ManageConnection", "Directory", "ManageConnection",
            "administration", "Administration", "الإدارة",
            "directory-integration", "Directory integration", "تكامل الدليل", PermissionLevel.Action,
            "Create or Update Directory Connections", "إنشاء اتصالات الدليل أو تحديثها", "Create and update directory connections and synchronization scope within the current tenant.", "إنشاء اتصالات الدليل وتحديثها ونطاق المزامنة ضمن المستأجر الحالي.",
            "Medium", false, ["Directory.View"], true, PermissionScope.Tenant),
        new(
            "b0000243-0000-0000-0000-000000000243", "Planning.Edit", "Planning", "Edit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "planning", "Planning", "التخطيط", PermissionLevel.Action,
            "Edit project plans", "تعديل خطط المشاريع", "Edit existing plan activities, dependencies, assignments, and calendars within project scope.", "تعديل الأنشطة والارتباطات والتعيينات والتقاويم الموجودة في الخطة ضمن نطاق المشروع.",
            "High", true, ["Planning.View"], true, PermissionScope.Project),
        new(
            "b0000286-0000-0000-0000-000000000286", "ProjectPlan.Create", "ProjectPlan", "Create",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "planning", "Planning", "التخطيط", PermissionLevel.Action,
            "Create project plans", "إنشاء خطط المشاريع", "Create a Project Plan within the actor's authorized project scope.", "إنشاء خطة مشروع ضمن نطاق المشروع المصرح به للمستخدم.",
            "High", true, ["Planning.View"], true, PermissionScope.Project),
        new(
            "b0000272-0000-0000-0000-000000000272", "ProjectSettings.View", "ProjectSettings", "View",
            "project-management", "Project Management", "إدارة المشاريع",
            "settings", "Settings", "الإعدادات", PermissionLevel.View,
            "Project Settings · View", "إعدادات المشروع · عرض", "Open Project Settings within the actor's authorized Tenant and Project scope. This permission does not allow saving changes or replace permissions required for protected settings.", "عرض إعدادات المشروع ضمن نطاق المستأجر والمشروع المصرح به للمستخدم. لا تمنح هذه الصلاحية حفظ التغييرات أو تحل محل الصلاحيات المطلوبة للإعدادات المحمية.",
            "Low", false, [], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000273-0000-0000-0000-000000000273", "ProjectSettings.Edit", "ProjectSettings", "Edit",
            "project-management", "Project Management", "إدارة المشاريع",
            "settings", "Settings", "الإعدادات", PermissionLevel.Action,
            "Project Settings · Edit", "إعدادات المشروع · تعديل", "Edit Project Settings within the actor's authorized Tenant and Project scope. Financial approval defaults and other protected settings continue to require their specific permissions.", "تعديل إعدادات المشروع ضمن نطاق المستأجر والمشروع المصرح به للمستخدم. تظل الإعدادات المالية المحمية وغيرها خاضعة للصلاحيات المخصصة لها.",
            "Medium", true, ["ProjectSettings.View"], true, PermissionScope.Project, IsAssignable: true, IsTenantAdminBaseline: false),
        new(
            "b0000280-0000-0000-0000-000000000280", "ApprovalRequest.View", "ApprovalRequest", "View",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.View,
            "View approval requests", "عرض طلبات الاعتماد", "View approval requests within tenant and project scope; assignment-filtered for reviewers.", "عرض طلبات الاعتماد ضمن نطاق المستأجر والمشروع؛ تُصفى قوائم المراجعين حسب الإسناد.",
            "Low", false, [], true),
        new(
            "b0000281-0000-0000-0000-000000000281", "ApprovalRequest.Submit", "ApprovalRequest", "Submit",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.Action,
            "Submit approval request", "إرسال طلب اعتماد", "Submit a draft approval request to its workflow.", "إرسال مسودة طلب اعتماد إلى مساره المعتمد.",
            "Medium", true, ["ApprovalRequest.View"], true),
        new(
            "b0000282-0000-0000-0000-000000000282", "ApprovalRequest.Approve", "ApprovalRequest", "Approve",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.Action,
            "Approve assigned requests", "اعتماد الطلبات المسندة", "Approve an assigned approval request; separation of duties applies.", "اعتماد طلب اعتماد مُسند؛ يطبق الفصل بين المهام.",
            "High", true, ["ApprovalRequest.View"], true),
        new(
            "b0000283-0000-0000-0000-000000000283", "ApprovalRequest.Reject", "ApprovalRequest", "Reject",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.Action,
            "Reject approval request", "رفض طلب اعتماد", "Reject an assigned approval request with a reason.", "رفض طلب اعتماد مُسند مع إبداء سبب.",
            "High", true, ["ApprovalRequest.View"], true),
        new(
            "b0000284-0000-0000-0000-000000000284", "ApprovalRequest.Delegate", "ApprovalRequest", "Delegate",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.Action,
            "Delegate approval request", "تفويض طلب اعتماد", "Delegate an assigned approval request to another user.", "تفويض طلب اعتماد مُسند إلى مستخدم آخر.",
            "Medium", true, ["ApprovalRequest.View"], true),
        new(
            "b0000285-0000-0000-0000-000000000285", "ApprovalRequest.Cancel", "ApprovalRequest", "Cancel",
            "project-delivery", "Project Delivery", "تسليم المشاريع",
            "approvals", "Approvals", "طلبات الاعتماد", PermissionLevel.Action,
            "Cancel approval request", "إلغاء طلب اعتماد", "Cancel a draft or submitted approval request.", "إلغاء طلب اعتماد مسود أو مُرسل.",
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

