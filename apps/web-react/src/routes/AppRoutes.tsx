import * as React from "react";
import { Navigate, Route, Routes, Outlet } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { RequireAuth } from "../auth/RequireAuth";
import { RequirePermission } from "../auth/RequirePermission";
import { Permissions } from "../lib/permissions";
// Eager: migration order 1–6 core (Home, Projects, My Work, Resources, Vendors, Teams)
import { HomePage } from "../features/home/HomePage";
import { LoginPage } from "../features/home/LoginPage";
import { ProjectsListPage } from "../features/projects/ProjectsListPage";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { ProjectTasksTab } from "../features/projects/ProjectTasksTab";
import { ProjectRisksTab } from "../features/projects/ProjectRisksTab";
import { ProjectFinanceTab } from "../features/projects/ProjectFinanceTab";
import { ProjectMembersTab } from "../features/projects/ProjectMembersTab";
import { ProjectDocumentsTab } from "../features/projects/ProjectDocumentsTab";
import { ProjectPlanningTab } from "../features/projects/ProjectPlanningTab";
import { ProjectWorkflowsTab } from "../features/projects/ProjectWorkflowsTab";
import { MyWorkLayout } from "../features/my-work/MyWorkLayout";
import { MyWorkTasksPage } from "../features/my-work/MyWorkTasksPage";
import { MyWorkApprovalsPage } from "../features/my-work/MyWorkApprovalsPage";
import { MyWorkTimesheetsPage } from "../features/my-work/MyWorkTimesheetsPage";
import { ResourcesListPage } from "../features/resources/ResourcesListPage";
import { VendorsPage } from "../features/vendors/VendorsPage";
import { TeamsPage } from "../features/teams/TeamsPage";
import { AcceptInvitationPage, StakeholderProjectsPage } from "../features/stakeholder/StakeholderPages";
import { WorkItemsPageFull, WorkItemDetailPageFull } from "../features/delivery/WorkItemPages";

// Lazy: heavy/secondary modules split out of the initial bundle (visible-first + code-split).
const CapacityPage = React.lazy(() => import("../features/resources/CapacityPage").then((m) => ({ default: m.CapacityPage })));
const CostUtilizationPage = React.lazy(() => import("../features/resources/CostUtilizationPage").then((m) => ({ default: m.CostUtilizationPage })));
const OrganizationPage = React.lazy(() => import("../features/organization/OrganizationPage").then((m) => ({ default: m.OrganizationPage })));
const RisksPage = React.lazy(() => import("../features/risks/RisksPage").then((m) => ({ default: m.RisksPage })));
const RiskDetailPage = React.lazy(() => import("../features/risks/RiskDetailPage").then((m) => ({ default: m.RiskDetailPage })));
const IssuesPage = React.lazy(() => import("../features/risks/IssuesPage").then((m) => ({ default: m.IssuesPage })));
const CostsPage = React.lazy(() => import("../features/finance/CostsPage").then((m) => ({ default: m.CostsPage })));
const WorkflowsPage = React.lazy(() => import("../features/workflow/WorkflowsPage").then((m) => ({ default: m.WorkflowsPage })));
const WorkflowDetailPage = React.lazy(() => import("../features/workflow/WorkflowDetailPage").then((m) => ({ default: m.WorkflowDetailPage })));
const AdministrationHub = React.lazy(() => import("../features/administration/AdministrationHub").then((m) => ({ default: m.AdministrationHub })));
const AdministrationLayout = React.lazy(() => import("../features/administration/AdministrationHub").then((m) => ({ default: m.AdministrationLayout })));
const AccessPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.AccessPage })));
const ReferenceDataPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.ReferenceDataPage })));
const PlatformSettingsPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.PlatformSettingsPage })));
const IntegrationsPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.IntegrationsPage })));
const GovernancePage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.GovernancePage })));
const CurrenciesPage = React.lazy(() => import("../features/administration/AdminPages").then((m) => ({ default: m.CurrenciesPage })));
const LicensePage = React.lazy(() => import("../features/administration/LicensePluginsPages").then((m) => ({ default: m.LicensePage })));
const PluginsPage = React.lazy(() => import("../features/administration/LicensePluginsPages").then((m) => ({ default: m.PluginsPage })));
const AccountHub = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountHub })));
const AccountLayout = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountLayout })));
const ProfilePage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.ProfilePage })));
const PreferencesPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.PreferencesPage })));
const SecurityPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.SecurityPage })));
const AccountNotificationsPage = React.lazy(() => import("../features/account/AccountPages").then((m) => ({ default: m.AccountNotificationsPage })));
const NotificationsPage = React.lazy(() => import("../features/notifications/NotificationsPage").then((m) => ({ default: m.NotificationsPage })));
const MeetingsPageFull = React.lazy(() => import("../features/delivery/MeetingPages").then((m) => ({ default: m.MeetingsPageFull })));
const MeetingDetailPageFull = React.lazy(() => import("../features/delivery/MeetingPages").then((m) => ({ default: m.MeetingDetailPageFull })));
const DocumentsPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.DocumentsPage })));
const PortfoliosPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.PortfoliosPage })));
const PortfolioDetailPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.PortfolioDetailPage })));
const ProgramsPage = React.lazy(() => import("../features/delivery/DeliveryPages").then((m) => ({ default: m.ProgramsPage })));
const DeferredPage = React.lazy(() => import("../features/delivery/DeferredPage").then((m) => ({ default: m.DeferredPage })));


function Suspended({ children }: { children: React.ReactNode }) {
  return <React.Suspense fallback={<div className="flex min-h-[40vh] items-center justify-center" role="status" aria-label="Loading"><div className="h-6 w-6 animate-spin rounded-full border-2 border-muted border-t-primary" /></div>}>{children}</React.Suspense>;
}

function Guard({ permissions, children }: { permissions: string | string[]; children: React.ReactNode }) {
  return <RequirePermission permissions={permissions}>{children}</RequirePermission>;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/home" replace />} />
      <Route path="/login" element={<LoginPage />} />
      {/* Public stakeholder invitation (no auth, no shell) */}
      <Route path="/stakeholder/accept-invitation" element={<AcceptInvitationPage />} />
      <Route element={<RequireAuth><AppShell><Outlet /></AppShell></RequireAuth>}>
        <Route path="/home" element={<HomePage />} />

        <Route path="/projects" element={<Guard permissions={Permissions.Projects.Read}><ProjectsListPage /></Guard>} />
        <Route path="/projects/:id" element={<Guard permissions={Permissions.Projects.Read}><ProjectDetailPage /></Guard>}>
          <Route index element={<Navigate to="overview" replace />} />
          <Route path="overview" element={<ProjectOverviewTab />} />
          <Route path="tasks" element={<ProjectTasksTab />} />
          <Route path="risks" element={<ProjectRisksTab />} />
          <Route path="finance" element={<ProjectFinanceTab />} />
          <Route path="members" element={<ProjectMembersTab />} />
          <Route path="documents" element={<ProjectDocumentsTab />} />
          <Route path="planning" element={<ProjectPlanningTab />} />
          <Route path="workflows" element={<ProjectWorkflowsTab />} />
          {/* Legacy Next deep-links → canonical tabs */}
          <Route path="plan" element={<Navigate to="../planning" replace />} />
          <Route path="settings/workflows" element={<Navigate to="../workflows" replace />} />
          <Route path="snapshots/:snapshotId" element={<Navigate to="../overview" replace />} />
          <Route path="board" element={<Navigate to="../tasks" replace />} />
          <Route path="matrix" element={<Navigate to="../tasks" replace />} />
          <Route path="gantt" element={<Navigate to="../planning" replace />} />
          <Route path="templates" element={<Navigate to="/projects" replace />} />
        </Route>

        <Route path="/my-work" element={<MyWorkLayout />}>
          <Route index element={<Navigate to="tasks" replace />} />
          <Route path="tasks" element={<MyWorkTasksPage />} />
          <Route path="approvals" element={<MyWorkApprovalsPage />} />
          <Route path="timesheets" element={<MyWorkTimesheetsPage />} />
        </Route>

        <Route path="/resources" element={<Guard permissions={Permissions.Resources.View}><ResourcesListPage /></Guard>} />
        <Route path="/resources/vendors" element={<Guard permissions={Permissions.Resources.View}><VendorsPage /></Guard>} />
        <Route path="/resources/teams" element={<Guard permissions={Permissions.Team.View}><TeamsPage /></Guard>} />
        <Route path="/resources/capacity" element={<Guard permissions={Permissions.Resources.View}><Suspended><CapacityPage /></Suspended></Guard>} />
        <Route path="/resources/cost-utilization" element={<Guard permissions={Permissions.Resources.View}><Suspended><CostUtilizationPage /></Suspended></Guard>} />
        <Route path="/resources/utilization" element={<Navigate to="/resources/cost-utilization" replace />} />

        <Route path="/portfolios" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfoliosPage /></Suspended></Guard>} />
        <Route path="/portfolios/programs" element={<Guard permissions={Permissions.Program.View}><Suspended><ProgramsPage /></Suspended></Guard>} />
        <Route path="/portfolios/programs/:id" element={<Guard permissions={Permissions.Program.View}><Suspended><PortfolioDetailPage /></Suspended></Guard>} />
        <Route path="/portfolios/optimization" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfoliosPage /></Suspended></Guard>} />
        <Route path="/portfolios/:id" element={<Guard permissions={Permissions.Portfolio.View}><Suspended><PortfolioDetailPage /></Suspended></Guard>} />

        <Route path="/meetings" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />
        <Route path="/meetings/:id" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingDetailPageFull /></Suspended></Guard>} />
        <Route path="/meetings/:id/minutes" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingDetailPageFull /></Suspended></Guard>} />
        <Route path="/meetings/minutes" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />
        <Route path="/meetings/actions" element={<Guard permissions={Permissions.Meeting.View}><Suspended><MeetingsPageFull /></Suspended></Guard>} />

        <Route path="/work-items" element={<Guard permissions={Permissions.WorkItem.View}><WorkItemsPageFull /></Guard>} />
        <Route path="/work-items/:id" element={<Guard permissions={Permissions.WorkItem.View}><WorkItemDetailPageFull /></Guard>} />

        <Route path="/risks-issues/risks" element={<Guard permissions={Permissions.Risk.View}><Suspended><RisksPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/new" element={<Guard permissions={Permissions.Risk.View}><Suspended><RisksPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/dashboard" element={<Guard permissions={Permissions.Risk.View}><Suspended><RisksPage /></Suspended></Guard>} />
        <Route path="/risks-issues/risks/:id" element={<Guard permissions={Permissions.Risk.View}><Suspended><RiskDetailPage /></Suspended></Guard>} />
        <Route path="/risks-issues/issues" element={<Guard permissions={Permissions.WorkItem.View}><Suspended><IssuesPage /></Suspended></Guard>} />
        <Route path="/risks-issues/matrix" element={<Guard permissions={Permissions.Risk.View}><Suspended><RisksPage /></Suspended></Guard>} />
        <Route path="/risks-issues" element={<Navigate to="/risks-issues/risks" replace />} />

        <Route path="/costs" element={<Guard permissions={Permissions.Finances.Read}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/budgets" element={<Guard permissions={Permissions.Finances.Read}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/actuals" element={<Guard permissions={Permissions.Finances.Read}><Suspended><CostsPage /></Suspended></Guard>} />
        <Route path="/costs/forecasts" element={<Guard permissions={Permissions.Finances.Read}><Suspended><CostsPage /></Suspended></Guard>} />

        <Route path="/documents" element={<Guard permissions={Permissions.Document.View}><Suspended><DocumentsPage /></Suspended></Guard>} />

        <Route path="/workflows" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowsPage /></Suspended></Guard>} />
        <Route path="/workflows/:id" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowDetailPage /></Suspended></Guard>} />
        <Route path="/workflows/:id/versions/:versionId" element={<Guard permissions={Permissions.Workflow.View}><Suspended><WorkflowDetailPage /></Suspended></Guard>} />

        <Route path="/stakeholder/projects" element={<Suspended><StakeholderProjectsPage /></Suspended>} />
        <Route path="/stakeholder/projects/:projectId" element={<Suspended><StakeholderProjectsPage /></Suspended>} />

        <Route path="/administration" element={<Suspended><AdministrationHub /></Suspended>} />
        <Route element={<Suspended><AdministrationLayout /></Suspended>}>
          <Route path="/administration/organization" element={<OrganizationPage />} />
          <Route path="/administration/access" element={<Guard permissions={[Permissions.Users.Read, Permissions.Settings.Read]}><Suspended><AccessPage /></Suspended></Guard>} />
          <Route path="/administration/users" element={<Navigate to="/administration/access" replace />} />
          <Route path="/administration/roles" element={<Navigate to="/administration/access" replace />} />
          <Route path="/administration/reference-data" element={<Suspended><ReferenceDataPage /></Suspended>} />
          <Route path="/administration/reference-data/project-roles" element={<Suspended><ReferenceDataPage /></Suspended>} />
          <Route path="/administration/reference-data/vendor-categories" element={<Suspended><ReferenceDataPage /></Suspended>} />
          <Route path="/administration/reference-data/work-item-statuses" element={<Suspended><ReferenceDataPage /></Suspended>} />
          <Route path="/administration/settings" element={<Suspended><PlatformSettingsPage /></Suspended>} />
          <Route path="/administration/integrations" element={<Suspended><IntegrationsPage /></Suspended>} />
          <Route path="/administration/governance" element={<Suspended><GovernancePage /></Suspended>} />
          <Route path="/administration/currencies" element={<Suspended><CurrenciesPage /></Suspended>} />
          <Route path="/administration/license" element={<Suspended><LicensePage /></Suspended>} />
          <Route path="/administration/plugins" element={<Suspended><PluginsPage /></Suspended>} />
          <Route path="/administration/personalization" element={<Navigate to="/account/preferences" replace />} />
        </Route>

        <Route path="/account" element={<Suspended><AccountHub /></Suspended>} />
        <Route element={<Suspended><AccountLayout /></Suspended>}>
          <Route path="/account/profile" element={<Suspended><ProfilePage /></Suspended>} />
          <Route path="/account/preferences" element={<Suspended><PreferencesPage /></Suspended>} />
          <Route path="/account/security" element={<Suspended><SecurityPage /></Suspended>} />
          <Route path="/account/notifications" element={<Suspended><AccountNotificationsPage /></Suspended>} />
        </Route>

        <Route path="/notifications" element={<Suspended><NotificationsPage /></Suspended>} />
        <Route path="/integrations" element={<Suspended><IntegrationsPage /></Suspended>} />

        {/* Experimental/deferred Next.js areas: deep-link safe, explicitly deferred */}
        <Route path="/reports" element={<Suspended><DeferredPage reasonKey="nav.deferredReports" /></Suspended>} />
        <Route path="/reports/*" element={<Suspended><DeferredPage reasonKey="nav.deferredReports" /></Suspended>} />
        <Route path="/ai-workspace" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/ai-workspace/*" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/security-audit" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/security-audit/*" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/tickets" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/test-management" element={<Suspended><DeferredPage reasonKey="nav.deferredAnalytics" /></Suspended>} />
        <Route path="/plugins" element={<Suspended><PluginsPage /></Suspended>} />
      </Route>
      <Route path="*" element={<Navigate to="/home" replace />} />
    </Routes>
  );
}
