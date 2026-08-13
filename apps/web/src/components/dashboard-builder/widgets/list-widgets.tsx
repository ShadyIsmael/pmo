import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { getProjectsAttention, getMyTodoTasks, getUpcomingMilestones, getTopRisks, getPendingApprovals, getUpcomingMeetings, getUnreadNotifications } from "@/lib/dashboard-mock-data";
import { getHealthDotColor } from "@/lib/utils";

function ProjectsAttentionList({ config }: { config: WidgetConfig }) {
  const projects = getProjectsAttention().slice(0, config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {projects.map((p) => (
        <div key={p.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-destructive/5 cursor-pointer transition-colors border-l-2 border-destructive text-xs">
          <span className={`h-2 w-2 shrink-0 rounded-full ${getHealthDotColor(p.health)}`} />
          <span className="flex-1 truncate font-medium">{p.name}</span>
          <span className="text-destructive tabular-nums">{p.scheduleVariance.toFixed(0)}%</span>
        </div>
      ))}
      {projects.length === 0 && <p className="text-xs text-muted-foreground text-center py-4">No projects require attention</p>}
    </div>
  );
}

function MyTodoList({ config }: { config: WidgetConfig }) {
  const tasks = getMyTodoTasks().slice(0, config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {tasks.map((t) => (
        <div key={t.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <input type="checkbox" className="h-3.5 w-3.5 rounded border-muted-foreground/30" readOnly />
          <span className="flex-1 truncate">{t.title}</span>
          <span className={`shrink-0 px-1 py-0.5 rounded text-[9px] font-medium ${t.priority === "Critical" ? "bg-destructive/10 text-destructive" : t.priority === "High" ? "bg-warning/10 text-warning" : "bg-muted/50 text-muted-foreground"}`}>{t.priority}</span>
        </div>
      ))}
    </div>
  );
}

function UpcomingMilestonesList({ config }: { config: WidgetConfig }) {
  const milestones = getUpcomingMilestones(config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {milestones.map((m) => (
        <div key={m.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
          <div className="flex-1 min-w-0">
            <div className="truncate font-medium">{m.title}</div>
            <div className="text-[10px] text-muted-foreground">{m.endDate}</div>
          </div>
          <span className={`shrink-0 text-[9px] px-1.5 py-0.5 rounded font-medium ${m.status === "Completed" ? "bg-success/10 text-success" : m.status === "In Progress" ? "bg-warning/10 text-warning" : "bg-muted/50 text-muted-foreground"}`}>{m.status}</span>
        </div>
      ))}
    </div>
  );
}

function TopRisksList({ config }: { config: WidgetConfig }) {
  const risks = getTopRisks(config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {risks.map((r) => (
        <div key={r.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <span className={`h-2 w-2 shrink-0 rounded-full ${r.score >= 15 ? "bg-destructive" : r.score >= 8 ? "bg-warning" : "bg-success"}`} />
          <div className="flex-1 min-w-0">
            <div className="truncate font-medium">{r.title}</div>
            <div className="text-[10px] text-muted-foreground">{r.category} {"\u00B7"} Score: {r.score}</div>
          </div>
          <span className={`shrink-0 text-[9px] px-1.5 py-0.5 rounded font-medium ${r.status === "Open" ? "bg-destructive/10 text-destructive" : "bg-warning/10 text-warning"}`}>{r.status}</span>
        </div>
      ))}
    </div>
  );
}

function PendingApprovalsList({ config }: { config: WidgetConfig }) {
  const approvals = getPendingApprovals();
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {approvals.map((a) => (
        <div key={a.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-warning/10 text-warning">
            <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
          <div className="flex-1 min-w-0">
            <div className="truncate font-medium">{a.title}</div>
            <div className="text-[10px] text-muted-foreground">{a.type} {"\u00B7"} {a.urgency}</div>
          </div>
          <span className={`shrink-0 text-[9px] px-1.5 py-0.5 rounded font-medium ${a.status === "Pending" ? "bg-warning/10 text-warning" : a.status === "Approved" ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive"}`}>{a.status}</span>
        </div>
      ))}
    </div>
  );
}

function UpcomingMeetingsList({ config }: { config: WidgetConfig }) {
  const meetings = getUpcomingMeetings(config.maxRecords || 8);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {meetings.map((m) => (
        <div key={m.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>
          </div>
          <div className="flex-1 min-w-0">
            <div className="truncate font-medium">{m.title}</div>
            <div className="text-[10px] text-muted-foreground">{m.startDate} {"\u00B7"} {m.attendees.length} attendees</div>
          </div>
        </div>
      ))}
    </div>
  );
}

function RecentNotificationsList({ config }: { config: WidgetConfig }) {
  const notifications = getUnreadNotifications();
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {notifications.map((n) => (
        <div key={n.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full ${n.type === "alert" ? "bg-destructive/10 text-destructive" : "bg-primary/10 text-primary"}`}>
            <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>
          </div>
          <div className="flex-1 min-w-0">
            <div className="truncate font-medium">{n.title}</div>
            <div className="text-[10px] text-muted-foreground truncate">{n.description}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

registerWidget("projects-attention", {
  name: "Projects Requiring Attention", description: "Red-health projects needing immediate action", category: "Projects", icon: "AlertTriangle",
  supportedPOVs: ["Executive", "Portfolio Manager", "Project Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.portfolio, f.program, f.owner],
}, ProjectsAttentionList);

registerWidget("my-todo", {
  name: "My To-do List", description: "Your outstanding tasks and action items", category: "My Work", icon: "ListChecks",
  supportedPOVs: ["Team Member", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.sortBy, f.includeCompleted],
}, MyTodoList);

registerWidget("upcoming-milestones", {
  name: "Upcoming Milestones", description: "Next project milestones and deadlines", category: "Schedule", icon: "Milestone",
  supportedPOVs: ["Project Manager", "Portfolio Manager", "Team Member", "Executive"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.portfolio, f.project, f.sortBy],
}, UpcomingMilestonesList);

registerWidget("top-risks", {
  name: "Top Risks", description: "Highest-scoring risks across the portfolio", category: "Risks", icon: "AlertTriangle",
  supportedPOVs: ["Executive", "Project Manager", "Portfolio Manager", "Finance Controller"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.owner, f.sortBy],
  dataSourceLabel: "Mock data",
}, TopRisksList);

registerWidget("pending-approvals", {
  name: "Pending Approvals", description: "Items awaiting your approval decision", category: "Approvals", icon: "CheckCircle",
  supportedPOVs: ["Project Manager", "Portfolio Manager", "Finance Controller", "PMO Administrator", "Team Member"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords],
}, PendingApprovalsList);

registerWidget("upcoming-meetings", {
  name: "Upcoming Meetings", description: "Scheduled meetings and governance sessions", category: "Meetings", icon: "Calendar",
  supportedPOVs: ["Team Member", "Project Manager", "Portfolio Manager", "Executive"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.sortBy],
}, UpcomingMeetingsList);

registerWidget("recent-notifications", {
  name: "Recent Notifications", description: "Your latest alerts and updates", category: "Notifications", icon: "Bell",
  supportedPOVs: ["Team Member", "Project Manager", "Resource Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords],
  dataSourceLabel: "Mock data",
}, RecentNotificationsList);
