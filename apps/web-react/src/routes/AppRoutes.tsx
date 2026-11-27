import * as React from "react";
import { Navigate, Route, Routes, Outlet } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { RequireAuth, RequireManagementAuthorization } from "../auth/RequireAuth";
import { RequirePermission } from "../auth/RequirePermission";
import { RequireFreshPermission } from "../auth/RequireFreshPermission";
import { DirectoryAccessPermissions, Permissions, ProjectLifecycleCatalogAccessPermissions, ReferenceDataAccessPermissions } from "../lib/permissions";
import { useI18n } from "../i18n/messages";
// Eager: migration order 1–6 core (Home, Projects, My Work, Resources, Vendors, Teams)
import { HomePage } from "../features/home/HomePage";
import { LoginPage } from "../features/home/LoginPage";
import { ChangeInitialPasswordPage } from "../features/account/ChangeInitialPasswordPage";
import { ProjectsListPage } from "../features/projects/ProjectsListPage";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { ProjectReportingConfigurationPage } from "../features/projects/ProjectReportingConfigurationPage";
import { ProjectTasksTab } from "../features/projects/ProjectTasksTab";
import { ProjectRisksTab } from "../features/projects/ProjectRisksTab";
import { ProjectFinanceTab } from "../features/projects/ProjectFinanceTab";
import { ProjectMembersTab } from "../features/projects/ProjectMembersTab";
import { ProjectDocumentsTab } from "../features/projects/ProjectDocumentsTab";
import { ProjectPlanningTabFull } from "../features/projects/ProjectPlanningTabFull";
import { ProjectChangeTrackerTab, ProjectHandoverTab, ProjectHistoryTab, ProjectMeetingsTab, ProjectSnapshotDetailPage, ProjectSnapshotsTab, ProjectStatusReportsTab } from "../features/projects/ProjectGovernanceTabs";
import { MyWorkLayout } from "../features/my-work/MyWorkLayout";
import { MyWorkTasksPage } from "../features/my-work/MyWorkTasksPage";
import { MyWorkApprovalsPage } from "../features/my-work/MyWorkApprovalsPage";
import { ApprovalReviewPage } from "../features/my-work/ApprovalReviewPage";
import { MyWorkTimesheetsPage } from "../features/my-work/MyWorkTimesheetsPage";
import { ResourcesListPage } from "../features/resources/ResourcesListPage";
import { VendorsPage } from "../features/vendors/VendorsPage";
import { TeamsPage } from "../features/teams/TeamsPage";
import { AcceptInvitationPage, StakeholderProjectDetailPage, StakeholderProjectsPage } from "../features/stakeholder/StakeholderPages";
import { WorkItemsPageFull, WorkItemDetailPageFull } from "../features/delivery/WorkItemPages";

// Lazy: heavy/secondary modules split out of the initial bundle (visible-first + code-split).
const CapacityPage = React.lazy(() => import("../features/resources/CapacityPage").then((m) => ({ default: m.CapacityPage })));
const ResourceUtilizationPage = React.lazy(() => import("../features/resources/ResourceUtilizationPage").then((m) => ({ default: m.ResourceUtilizationPage })));
const CostUtilizationPage = React.lazy(() => import("../features/resources/CostUtilizationPage").then((m) => ({ default: m.CostUtilizationPage })));
const OrganizationPage = React.lazy(() => import("../features/organization/OrganizationPage").then((m) => ({ default: m.OrganizationPage })));
const RisksPage = React.lazy(() => import("../features/risks/RisksPage").then((m) => ({ default: m.RisksPage })));
const RiskCreatePage = React.lazy(() => import("../features/risks/RiskCreatePage").then((m) => ({ default: m.RiskCreatePage })));
const RiskDetailPage = React.lazy(() => import("../features/risks/RiskDetailPage").then((m) => ({ default: m.RiskDetailPage })));
const RiskActionFollowUpPage = React.lazy(() => import("../features/risks/RiskActionFollowUpPage").then((m) => ({ default: m.RiskActionFollowUpPage })));
const RiskDashboardPage = React.lazy(() => import("../features/risks/RiskDashboardPage").then((m) => ({ default: m.RiskDashboardPage })));
const RiskMatrixPage = React.lazy(() => import("../features/risks/RiskMatrixPage").then((m) => ({ default: m.RiskMatrixPage })));
const IssuesPage = React.lazy(() => import("../features/risks/IssuesPage").then((m) => ({ default: m.IssuesPage })));
const CostsPage = React.lazy(() => import("../features/finance/CostsPage").then((m) => ({ default: m.CostsPage })));
const PlannedPaymentsPage = React.lazy(() => import("../features/finance/PlannedPaymentsPage").then((m) => ({ default: m.PlannedPaymentsPage })));
const WorkflowsPageFull = React.lazy(() => import("../features/workflow/WorkflowPagesFull").then((m) => ({ default: m.WorkflowsPageFull })));
const WorkflowDefinitionPageFull = React.lazy(() => import("../features/workflow/WorkflowPagesFull").then((m) => ({ default: m.WorkflowDefinitionPageFull })));
const WorkflowVersionPageFull = React.lazy(() => import("../features/workflow/WorkflowPagesFull").then((m) => ({ default: m.WorkflowVersionPageFull })));
const AdministrationHub = React.lazy(() => import("../features/administration/AdministrationHub").then((m) => ({ default: m.AdministrationHub })));
const AdministrationLayout = React.lazy(() => import("../features/administration/AdministrationHub").then((m) => ({ default: m.AdministrationLayout })));
const AccessPage = React.lazy(() => import("../features/administration/AccessPages").then((m) => ({ default: m.AccessPage })));
const AccessUsersPage = React.lazy(() => import("../features/administration/AccessPages").then((m) => ({ default: m.AccessUsersPage })));
const UserPermissionsPage = React.lazy(() => import("../features/administration/AccessPages").then((m) => ({ default: m.UserPermissionsPage })));
const RolesPage = React.lazy(() => import("../features/administration/RolesPage").then((m) => ({ default: m.RolesPage })));
const ReferenceDataPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.ReferenceDataPage })));
const DocumentCategoriesPage = React.lazy(() => import("../features/administration/DocumentCategoriesPage").then((m) => ({ default: m.DocumentCategoriesPage })));
const ProjectLifecycleCatalogPage = React.lazy(() => import("../features/administration/ProjectLifecycleCatalogPage").then((m) => ({ default: m.ProjectLifecycleCatalogPage })));
const VendorCategoriesPage = React.lazy(() => import("../features/administration/VendorCategoriesPage").then((m) => ({ default: m.VendorCategoriesPage })));
const ProjectRolesPage = React.lazy(() => import("../features/administration/ProjectRolesPage").then((m) => ({ default: m.ProjectRolesPage })));
const WorkItemStatusesPage = React.lazy(() => import("../features/administration/WorkItemStatusesPage").then((m) => ({ default: m.WorkItemStatusesPage })));
const PlatformGeneralSettingsPage = React.lazy(() => import("../features/administration/SettingsWorkspaces").then((m) => ({ default: m.PlatformGeneralSettingsPage })));
const TenantGeneralSettingsPage = React.lazy(() => import("../features/administration/SettingsWorkspaces").then((m) => ({ default: m.TenantGeneralSettingsPage })));
const OrganizationGeneralSettingsPage = React.lazy(() => import("../features/administration/SettingsWorkspaces").then((m) => ({ default: m.OrganizationGeneralSettingsPage })));
const EmailNotificationSettingsPage = React.lazy(() => import("../features/administration/EmailNotificationSettingsPage").then((m) => ({ default: m.EmailNotificationSettingsPage })));
const IntegrationsPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.IntegrationsPage })));
const DirectoryIntegrationPage = React.lazy(() => import("../features/administration/DirectoryIntegrationPage").then((m) => ({ default: m.DirectoryIntegrationPage })));
const GovernancePage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.GovernancePage })));
const EntityAssignmentRecoveryPage = React.lazy(() => import("../features/administration/EntityAssignmentRecoveryPage").then((m) => ({ default: m.EntityAssignmentRecoveryPage })));
const CurrenciesPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.CurrenciesPage })));
const LicensePage = React.lazy(() => import("../features/administration/LicensePluginsPages").then((m) => ({ default: m.LicensePage })));
const PluginsPage = React.lazy(() => import("../features/administration/LicensePluginsPages").then((m) => ({ default: m.PluginsPage })));
const AccountHub = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountHub })));
const AccountLayout = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountLayout })));
const ProfilePage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.ProfilePage })));
const MembershipsPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.MembershipsPage })));
const PreferencesPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.PreferencesPage })));
const SecurityPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.SecurityPage })));
const AccountNotificationsPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountNotificationsPage })));
const NotificationsPage = React.lazy(() => import("../features/notifications/NotificationsPage").then((m) => ({ default: m.NotificationsPage })));
const MeetingsPageFull = React.lazy(() => import("../features/delivery/MeetingPages").then((m) => ({ default: m.MeetingsPageFull })));
const MeetingDetailPageFull = React.lazy(() => import("../features/delivery/MeetingPages").then((m) => ({ default: m.MeetingDetailPageFull })));
const MeetingCreatePage = React.lazy(() => import("../features/delivery/MeetingCreatePage").then((m) => ({ default: m.MeetingCreatePage })));
const DocumentsPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.DocumentsPage })));
const ReportsPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.ReportsPageFull })));
const AuditLogPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.AuditLogPageFull })));
const SecurityAuditPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.SecurityAuditPageFull })));
const DataClassificationPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.DataClassificationPageFull })));
const SecurityPoliciesPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.SecurityPoliciesPageFull })));
const AIWorkspacePageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.AIWorkspacePageFull })));
const AIInsightsPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.AIInsightsPageFull })));
const AIPlanGeneratorPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.AIPlanGeneratorPageFull })));
const TicketsPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.WorkItemCollectionPageFull })));
const TestManagementPageFull = React.lazy(() => import("../features/operations/OperationsPagesFull").then((m) => ({ default: m.TestManagementPageFull })));
const PortfoliosPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.PortfoliosPage })));
const PortfolioDetailPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.PortfolioDetailPage })));
const PortfolioOptimizationPage = React.lazy(() => import("../features/delivery/PortfolioOptimizationPage").then((m) => ({ default: m.PortfolioOptimizationPage })));
const ProgramsPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.ProgramsPage })));
const ProgramDetailPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.ProgramDetailPage })));
const ProjectBoardPage = React.lazy(() => import("../features/projects/ProjectWorkspaceViews").then((m) => ({ default: m.ProjectBoardPage })));
const ProjectGanttPage = React.lazy(() => import("../features/projects/ProjectWorkspaceViews").then((m) => ({ default: m.ProjectGanttPage })));
const ProjectResourceMatrixPage = React.lazy(() => import("../features/projects/ProjectWorkspaceViews").then((m) => ({ default: m.ProjectResourceMatrixPage })));
const ProjectTemplatesPage = React.lazy(() => import("../features/projects/ProjectWorkspaceViews").then((m) => ({ default: m.ProjectTemplatesPage })));


function Suspended({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();
  return <React.Suspense fallback={<div className="flex min-h-[40vh] items-center justify-center" role="status" aria-label={t("common.loading")}><div className="h-6 w-6 animate-spin rounded-full border-2 border-muted border-t-primary" /></div>}>{children}</React.Suspense>;
}

function Guard({ permissions, children }: { permissions: string | string[]; children: React.ReactNode }) {
  return <RequirePermission permissions={permissions}>{children}</RequirePermission>;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/home" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/account/change-initial-password" element={<Suspended><RequireAuth><ChangeInitialPasswordPage /></RequireAuth></Suspended>} />
      {/* Public stakeholder invitation (no auth, no shell) */}
      <Route path="/stakeholder/accept-invitation" element={<AcceptInvitationPage />} />
      <Route element={<RequireAuth><RequireManagementAuthorization><AppShell><Outlet /></AppShell></RequireManagementAuthorization></RequireAuth>}>
        <Route path="/home" element={<Guard permissions={Permissions.Dashboard.View}><HomePage /></Guard>} />

        <Route path="/projects" element={<Guard permissions={Permissions.Project.View}><ProjectsListPage /></Guard>} />
        <Route path="/projects/board" element={<Guard permissions={Permissions.Project.View}><Guard permissions={Permissions.WorkItem.View}><Suspended><ProjectBoardPage /></Suspended></Guard></Guard>} />
        <Route path="/projects/gantt" element={<Guard permissions={Permissions.Project.View}><Guard permissions={Permissions.Planning.View}><Suspended><ProjectGanttPage /></Suspended></Guard></Guard>} />
        <Route path="/projects/matrix" element={<Guard permissions={Permissions.Project.View}><Guard permissions={Permissions.Resources.View}><Suspended><ProjectResourceMatrixPage /></Suspended></Guard></Guard>} />
        <Route path="/projects/templates" element={<Guard permissions={Permissions.Document.View}><Suspended><ProjectTemplatesPage /></Suspended></Guard>} />
        <Route path="/projects/:id/snapshots/:snapshotId" element={<Guard permissions={Permissions.Project.View}><ProjectSnapshotDetailPage /></Guard>} />
        <Route path="/projects/:id" element={<Guard permissions={Permissions.Project.View}><ProjectDetailPage /></Guard>}>
          <Route index element={<Navigate to="overview" replace />} />
          <Route path="overview" element={<ProjectOverviewTab />} />
          <Route path="plan" element={<ProjectPlanningTabFull />} />
          <Route path="team" element={<ProjectMembersTab />} />
          <Route path="financials" element={<Guard permissions={Permissions.ProjectFinance.View}><ProjectFinanceTab /></Guard>} />
          <Route path="settings" element={<RequirePermission permissions={Permissions.ProjectSettings.View}><Navigate to="../overview?openSettings=1" relative="route" replace /></RequirePermission>} />
          <Route path="risks-issues" element={<ProjectRisksTab />} />
          <Route path="status-reports" element={<Guard permissions={Permissions.StatusReport.View}><ProjectStatusReportsTab /></Guard>} />
          <Route path="settings/reporting" element={<Guard permissions={Permissions.StatusReport.ManageConfiguration}><ProjectReportingConfigurationPage /></Guard>} />
          <Route path="meetings" element={<ProjectMeetingsTab />} />
          <Route path="handover" element={<ProjectHandoverTab />} />
          <Route path="history" element={<ProjectHistoryTab />} />
          <Route path="snapshots" element={<ProjectSnapshotsTab />} />
          <Route path="change-tracker" element={<ProjectChangeTrackerTab />} />
          <Route path="tasks" element={<ProjectTasksTab />} />
          <Route path="risks" element={<ProjectRisksTab />} />
          <Route path="finance" element={<Guard permissions={Permissions.ProjectFinance.View}><ProjectFinanceTab /></Guard>} />
          <Route path="members" element={<ProjectMembersTab />} />
          <Route path="documents" element={<ProjectDocumentsTab />} />
          <Route path="planning" element={<ProjectPlanningTabFull />} />
          <Route path="workflows" element={<RequirePermission permissions={Permissions.ProjectSettings.View}><Navigate to="../overview?openSettings=1" relative="route" replace /></RequirePermission>} />
          {/* Legacy Next deep-links → canonical tabs */}
          <Route path="plan/legacy" element={<Navigate to="../plan" replace />} />
          <Route path="settings/workflows" element={<RequirePermission permissions={Permissions.ProjectSettings.View}><Navigate to="../overview?openSettings=1" relative="route" replace /></RequirePermission>} />
          <Route path="board" element={<Guard permissions={Permissions.WorkItem.View}><Suspended><ProjectBoardPage /></Suspended></Guard>} />
          <Route path="matrix" element={<Guard permissions={Permissions.Resources.View}><Suspended><ProjectResourceMatrixPage /></Suspended></Guard>} />
          <Route path="gantt" element={<Guard permissions={Permissions.Planning.View}><Suspended><ProjectGanttPage /></Suspended></Guard>} />
          <Route path="templates" element={<Navigate to="/projects" replace />} />
        </Route>

        <Route path="/my-work" element={<MyWorkLayout />}>
          <Route index element={<Navigate to="tasks" replace />} />
          <Route path="tasks" element={<MyWorkTasksPage />} />
          <Route path="approvals" element={<MyWorkApprovalsPage />} />
          <Route path="approvals/review/project-handover/:id" element={<ApprovalReviewPage type="project-handover" />} />
          <Route path="approvals/review/program-handover/:id" element={<ApprovalReviewPage type="program-handover" />} />
          <Route path="approvals/review/portfolio-handover/:id" element={<ApprovalReviewPage type="portfolio-handover" />} />
          <Route path="approvals/review/change-request/:id" element={<ApprovalReviewPage type="change-request" />} />
          <Route path="timesheets" element={<MyWorkTimesheetsPage />} />
        </Route>

        <Route path="/resources" element={<Guard permissions={Permissions.Resources.View}><ResourcesListPage /></Guard>} />
        <Route path="/resources/vendors" element={<Guard permissions={Permissions.Resources.View}><VendorsPage /></Guard>} />
        <Route path="/resources/teams" element={<Guard permissions={Permissions.Team.View}><TeamsPage /></Guard>} />
        <Route path="/resources/capacity" element={<Guard permissions={Permissions.Resources.View}><Suspended><CapacityPage /></Suspended></Guard>} />
        <Route path="/resources/utilization" element={<Guard permissions={Permissions.Resources.View}><Suspended><ResourceUtilizationPage /></Suspended></Guard>} />
        <Route path="/resources/cost-utilization" element={<Guard permissions={Permissions.Resources.View}><Suspended><CostUtilizationPage /></Suspended></Guard>} />

        <Route path="/portfolios" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfoliosPage /></Suspended></Guard>} />
        <Route path="/portfolios/programs" element={<Guard permissions={Permissions.Program.View}><Suspended><ProgramsPage /></Suspended></Guard>} />
        <Route path="/portfolios/programs/:id" element={<Guard permissions={Permissions.Program.View}><Suspended><ProgramDetailPage /></Suspended></Guard>} />
        <Route path="/portfolios/optimization" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfolioOptimizationPage /></Suspended></Guard>} />
        <Route path="/portfolios/:id" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfolioDetailPage /></Suspended></Guard>} />

        <Route path="/meetings" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />
        <Route path="/meetings/new" element={<Guard permissions={Permissions.Meeting.Create}><Suspended><MeetingCreatePage /></Suspended></Guard>} />
        <Route path="/meetings/:id" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingDetailPageFull /></Suspended></Guard>} />
        <Route path="/meetings/:id/minutes" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingDetailPageFull /></Suspended></Guard>} />
        <Route path="/meetings/minutes" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />
        <Route path="/meetings/actions" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />

        <Route path="/work-items" element={<Guard permissions={Permissions.WorkItem.View}><WorkItemsPageFull /></Guard>} />
        <Route path="/work-items/:id" element={<Guard permissions={Permissions.WorkItem.View}><WorkItemDetailPageFull /></Guard>} />

        <Route path="/risks-issues/risks" element={<Guard permissions={Permissions.Risk.View}><Suspended><RisksPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/new" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskCreatePage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/dashboard" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskDashboardPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risk-action-follow-up/:actionPublicId" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskActionFollowUpPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/:id" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskDetailPage /></Suspended></Guard>} />
        <Route path="/risks-issues/issues" element={<Guard permissions={Permissions.Issue.View}><Suspended><IssuesPage /></Suspended></Guard>} />
        <Route path="/risks-issues/matrix" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskMatrixPage /></Suspended></Guard>} />
        <Route path="/risks-issues" element={<Navigate to="/risks-issues/risks" replace />} />

        <Route path="/costs" element={<Guard permissions={Permissions.ProjectFinancialFoundation.View}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/budgets" element={<Guard permissions={Permissions.ProjectAnnualBudget.View}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/actuals" element={<Guard permissions={Permissions.Cost.View}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/payments" element={<Guard permissions={Permissions.PaymentObligation.View}><Suspended><PlannedPaymentsPage /></Suspended></Guard>} />
        <Route path="/costs/forecasts" element={<Guard permissions={Permissions.ProjectFinancialFoundation.View}><Suspended><CostsPage /></Suspended></Guard>} />

        <Route path="/documents" element={<Guard permissions={Permissions.Document.View}><Suspended><DocumentsPage /></Suspended></Guard>} />

        <Route path="/workflows" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowsPageFull /></Suspended></Guard>} />
        <Route path="/workflows/:id" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowDefinitionPageFull /></Suspended></Guard>} />
        <Route path="/workflows/:id/versions/:versionId" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowVersionPageFull /></Suspended></Guard>} />

        <Route path="/stakeholder/projects" element={<Suspended><StakeholderProjectsPage /></Suspended>} />
        <Route path="/stakeholder/projects/:projectId" element={<Suspended><StakeholderProjectDetailPage /></Suspended>} />

        <Route path="/administration" element={<Suspended><AdministrationHub /></Suspended>} />
        <Route element={<Suspended><AdministrationLayout /></Suspended>}>
          <Route path="/administration/organization" element={<Guard permissions={Permissions.Settings.View}><OrganizationPage /></Guard>} />
          <Route path="/administration/access" element={<Guard permissions={[Permissions.Users.View, Permissions.Users.ManageAccounts, Permissions.Users.SetStatus, Permissions.Users.AssignRoles, Permissions.Users.ManageOverrides, Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete]}><Suspended><AccessPage /></Suspended></Guard>} />
          <Route path="/administration/access/users" element={<Guard permissions={[Permissions.Users.View, Permissions.Users.ManageAccounts]}><Suspended><AccessUsersPage /></Suspended></Guard>} />
          <Route path="/administration/access/users/:userId/permissions" element={<Guard permissions={Permissions.Users.ManageOverrides}><Suspended><UserPermissionsPage /></Suspended></Guard>} />
          <Route path="/administration/access/roles" element={<Guard permissions={[Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete, Permissions.Users.View]}><Suspended><RolesPage /></Suspended></Guard>} />
          <Route path="/administration/users" element={<Navigate to="/administration/access/users" replace />} />
          <Route path="/administration/roles" element={<Navigate to="/administration/access/roles" replace />} />
          <Route path="/administration/reference-data" element={<Guard permissions={[...ReferenceDataAccessPermissions, Permissions.DocumentCategory.View, ...ProjectLifecycleCatalogAccessPermissions, Permissions.OrganizationConfiguration.View, Permissions.OrganizationConfiguration.Manage]}><Suspended><ReferenceDataPage /></Suspended></Guard>} />
          <Route path="/administration/reference-data/document-categories" element={<Guard permissions={Permissions.DocumentCategory.View}><Suspended><DocumentCategoriesPage /></Suspended></Guard>} />
          <Route path="/administration/reference-data/project-lifecycle" element={<Guard permissions={[...ProjectLifecycleCatalogAccessPermissions, Permissions.OrganizationConfiguration.View, Permissions.OrganizationConfiguration.Manage]}><Suspended><ProjectLifecycleCatalogPage /></Suspended></Guard>} />
          <Route path="/administration/reference-data/project-roles" element={<Guard permissions={[...ReferenceDataAccessPermissions, Permissions.Resources.View]}><Suspended><ProjectRolesPage /></Suspended></Guard>} />
          <Route path="/administration/reference-data/vendor-categories" element={<Guard permissions={ReferenceDataAccessPermissions}><Suspended><VendorCategoriesPage /></Suspended></Guard>} />
          <Route path="/administration/reference-data/work-item-statuses" element={<Guard permissions={ReferenceDataAccessPermissions}><Suspended><WorkItemStatusesPage /></Suspended></Guard>} />
          <Route path="/administration/settings" element={<Suspended><PlatformGeneralSettingsPage /></Suspended>} />
          <Route path="/administration/tenant-settings" element={<Guard permissions={Permissions.Settings.View}><Suspended><TenantGeneralSettingsPage /></Suspended></Guard>} />
          <Route path="/administration/organization-settings" element={<Guard permissions={Permissions.Settings.View}><Suspended><OrganizationGeneralSettingsPage /></Suspended></Guard>} />
          <Route path="/administration/email-notifications" element={<Suspended><EmailNotificationSettingsPage /></Suspended>} />
          <Route path="/administration/integrations" element={<Guard permissions={[Permissions.Settings.View, ...DirectoryAccessPermissions]}><Suspended><IntegrationsPage /></Suspended></Guard>} />
          <Route path="/administration/integrations/directory" element={<Guard permissions={DirectoryAccessPermissions}><Suspended><DirectoryIntegrationPage /></Suspended></Guard>} />
          <Route path="/administration/governance" element={<RequireFreshPermission permissions={[Permissions.Settings.View, Permissions.Workflow.View, Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage]}><Suspended><GovernancePage /></Suspended></RequireFreshPermission>} />
          <Route path="/administration/governance/entity-assignment-recovery" element={<RequireFreshPermission permissions={[Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage]}><Suspended><EntityAssignmentRecoveryPage /></Suspended></RequireFreshPermission>} />
          <Route path="/administration/currencies" element={<Guard permissions={ReferenceDataAccessPermissions}><Suspended><CurrenciesPage /></Suspended></Guard>} />
          <Route path="/administration/license" element={<Guard permissions={Permissions.Settings.View}><Suspended><LicensePage /></Suspended></Guard>} />
          <Route path="/administration/plugins" element={<Guard permissions={Permissions.Plugin.View}><Suspended><PluginsPage /></Suspended></Guard>} />
          <Route path="/administration/personalization" element={<Navigate to="/account/preferences" replace />} />
        </Route>

        <Route path="/account" element={<Suspended><AccountHub /></Suspended>} />
        <Route element={<Suspended><AccountLayout /></Suspended>}>
          <Route path="/account/profile" element={<Suspended><ProfilePage /></Suspended>} />
          <Route path="/account/memberships" element={<Suspended><MembershipsPage /></Suspended>} />
          <Route path="/account/preferences" element={<Suspended><PreferencesPage /></Suspended>} />
          <Route path="/account/security" element={<Suspended><SecurityPage /></Suspended>} />
          <Route path="/account/notifications" element={<Suspended><AccountNotificationsPage /></Suspended>} />
        </Route>

        <Route path="/notifications" element={<Suspended><NotificationsPage /></Suspended>} />
        <Route path="/integrations" element={<Guard permissions={Permissions.Settings.View}><Suspended><IntegrationsPage /></Suspended></Guard>} />

        <Route path="/reports" element={<Guard permissions={Permissions.Report.View}><Suspended><ReportsPageFull /></Suspended></Guard>} />
        <Route path="/reports/*" element={<Guard permissions={Permissions.Report.View}><Suspended><ReportsPageFull /></Suspended></Guard>} />
        <Route path="/ai-workspace" element={<Guard permissions={Permissions.Planning.View}><Suspended><AIWorkspacePageFull /></Suspended></Guard>} />
        <Route path="/ai-workspace/insights" element={<Guard permissions={Permissions.Planning.View}><Suspended><AIInsightsPageFull /></Suspended></Guard>} />
        <Route path="/ai-workspace/plan-generator" element={<Guard permissions={Permissions.Planning.View}><Suspended><AIPlanGeneratorPageFull /></Suspended></Guard>} />
        <Route path="/ai-workspace/*" element={<Guard permissions={Permissions.Planning.View}><Suspended><AIWorkspacePageFull /></Suspended></Guard>} />
        <Route path="/security-audit" element={<Guard permissions={Permissions.Audit.View}><Suspended><SecurityAuditPageFull /></Suspended></Guard>} />
        <Route path="/security-audit/audit-log" element={<Guard permissions={Permissions.Audit.View}><Suspended><AuditLogPageFull /></Suspended></Guard>} />
        <Route path="/security-audit/data-classification" element={<Guard permissions={Permissions.Audit.View}><Suspended><DataClassificationPageFull /></Suspended></Guard>} />
        <Route path="/security-audit/policies" element={<Guard permissions={Permissions.Audit.View}><Suspended><SecurityPoliciesPageFull /></Suspended></Guard>} />
        <Route path="/security-audit/*" element={<Guard permissions={Permissions.Audit.View}><Suspended><SecurityAuditPageFull /></Suspended></Guard>} />
        <Route path="/tickets" element={<Guard permissions={Permissions.WorkItem.View}><Suspended><TicketsPageFull type="Ticket" titleKey="operations.ticketsTitle" descriptionKey="operations.ticketsDescription" /></Suspended></Guard>} />
        <Route path="/test-management" element={<Guard permissions={Permissions.WorkItem.View}><Suspended><TestManagementPageFull /></Suspended></Guard>} />
        <Route path="/plugins" element={<Guard permissions={Permissions.Plugin.View}><Suspended><PluginsPage /></Suspended></Guard>} />
      </Route>
      <Route path="*" element={<Navigate to="/home" replace />} />
    </Routes>
  );
}
