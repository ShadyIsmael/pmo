export interface NavItem {
  label: string;
  labelAr?: string;
  path: string;
  icon: string;
  badge?: string;
  permissions?: readonly string[];
  children?: readonly NavItem[];
}

export interface NavSection {
  label: string;
  labelAr?: string;
  items: readonly NavItem[];
}

export interface NavigationConfig {
  sidebar: readonly NavSection[];
  topNav: readonly NavItem[];
}

export const navigationConfig: NavigationConfig = {
  sidebar: [
    {
      label: "Overview",
      labelAr: "نظرة عامة",
      items: [
        { label: "Dashboard", labelAr: "لوحة القيادة", path: "/dashboard", icon: "LayoutDashboard" },
        { label: "My Work", labelAr: "مهامي", path: "/my-work", icon: "ClipboardList" },
      ],
    },
    {
      label: "Management",
      labelAr: "الإدارة",
      items: [
        { label: "Portfolios", labelAr: "المحافظ", path: "/portfolios", icon: "Briefcase" },
        { label: "Programs", labelAr: "البرامج", path: "/programs", icon: "Layers" },
        { label: "Projects", labelAr: "المشاريع", path: "/projects", icon: "FolderKanban" },
      ],
    },
    {
      label: "Resources",
      labelAr: "الموارد",
      items: [
        { label: "Resources", labelAr: "الموارد", path: "/resources", icon: "Users" },
        { label: "Skills", labelAr: "المهارات", path: "/skills", icon: "Brain" },
        { label: "Rate Cards", labelAr: "بطاقات الأسعار", path: "/rate-cards", icon: "CreditCard" },
      ],
    },
    {
      label: "Financial",
      labelAr: "المالية",
      items: [
        { label: "Budgets", labelAr: "الميزانيات", path: "/budgets", icon: "Wallet" },
        { label: "Costs", labelAr: "التكاليف", path: "/costs", icon: "Receipt" },
        { label: "Invoices", labelAr: "الفواتير", path: "/invoices", icon: "FileText" },
      ],
    },
    {
      label: "Governance",
      labelAr: "الحوكمة",
      items: [
        { label: "Risks", labelAr: "المخاطر", path: "/risks", icon: "AlertTriangle" },
        { label: "Issues", labelAr: "المشكلات", path: "/issues", icon: "AlertCircle" },
        { label: "Meetings", labelAr: "الاجتماعات", path: "/meetings", icon: "Calendar" },
        { label: "Documents", labelAr: "المستندات", path: "/documents", icon: "File" },
        { label: "Audit Log", labelAr: "سجل التدقيق", path: "/audit", icon: "ScrollText" },
      ],
    },
    {
      label: "Settings",
      labelAr: "الإعدادات",
      items: [
        { label: "Organization", labelAr: "المؤسسة", path: "/settings/organization", icon: "Building2" },
        { label: "Users & Roles", labelAr: "المستخدمين والأدوار", path: "/settings/users", icon: "Shield" },
        { label: "Workflows", labelAr: "سير العمل", path: "/settings/workflows", icon: "GitBranch" },
        { label: "Integrations", labelAr: "التكاملات", path: "/settings/integrations", icon: "Plug" },
        { label: "Plugins", labelAr: "الإضافات", path: "/settings/plugins", icon: "Puzzle" },
      ],
    },
  ],
  topNav: [
    { label: "Search", labelAr: "بحث", path: "/search", icon: "Search" },
    { label: "Notifications", labelAr: "الإشعارات", path: "/notifications", icon: "Bell" },
    { label: "Profile", labelAr: "الملف الشخصي", path: "/profile", icon: "User" },
  ],
};
