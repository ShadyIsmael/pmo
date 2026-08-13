import {
  mockPortfolios, mockProjects, mockUsers, mockResources,
  mockTasks, mockRisks, mockIssues, mockMeetings,
  mockMeetingActions, mockApprovals, mockNotifications,
} from "@/lib/mock-data";

export function getActiveProjects() {
  return mockProjects.filter(
    (p) => p.status === "Active" || p.status === "At Risk" || p.status === "Delayed"
  );
}

export function getProjectsByHealth() {
  const active = getActiveProjects();
  return {
    green: active.filter((p) => p.health === "green").length,
    amber: active.filter((p) => p.health === "amber").length,
    red: active.filter((p) => p.health === "red").length,
  };
}

export function getProjectsByStatus() {
  const counts: Record<string, number> = {};
  mockProjects.forEach((p) => { counts[p.status] = (counts[p.status] || 0) + 1; });
  return counts;
}

export function getTotalBudget() {
  return getActiveProjects().reduce((s, p) => s + p.budget, 0);
}

export function getTotalActual() {
  return getActiveProjects().reduce((s, p) => s + p.actualCost, 0);
}

export function getTotalForecast() {
  return getActiveProjects().reduce((s, p) => s + p.forecast, 0);
}

export function getForecastVariancePercent() {
  const budget = getTotalBudget();
  const forecast = getTotalForecast();
  return budget > 0 ? ((forecast - budget) / budget) * 100 : 0;
}

export function getOpenRisks() {
  return mockRisks.filter((r) => r.status === "Open" || r.status === "Mitigating");
}

export function getCriticalRisks() {
  return getOpenRisks().filter((r) => r.score >= 15);
}

export function getOverdueTasks() {
  const today = new Date().toISOString().split("T")[0]!;
  return mockTasks.filter((t) => t.endDate < today && t.status !== "Completed").slice(0, 20);
}

export function getTasksDueToday() {
  const today = new Date().toISOString().split("T")[0]!;
  return mockTasks.filter((t) => t.endDate === today && t.status !== "Completed").slice(0, 20);
}

export function getCriticalTasks() {
  return mockTasks.filter((t) => t.priority === "Critical" && t.status !== "Completed").slice(0, 20);
}

export function getMyTodoTasks() {
  return mockTasks.filter((t) => t.status !== "Completed").slice(0, 15);
}

export function getDelayedProjects() {
  return getActiveProjects().filter((p) => p.scheduleVariance < 0);
}

export function getProjectsAttention() {
  return getActiveProjects().filter((p) => p.health === "red");
}

export function getProjectsOverBudget() {
  return getActiveProjects().filter((p) => p.costVariance < 0);
}

export function getUpcomingMilestones(limit = 10) {
  return mockTasks
    .filter((t) => t.isMilestone && t.status !== "Completed")
    .sort((a, b) => new Date(a.endDate).getTime() - new Date(b.endDate).getTime())
    .slice(0, limit);
}

export function getResourceUtilization() {
  const total = mockResources.reduce((s, r) => s + r.availability, 0);
  const allocated = mockResources.reduce((s, r) => s + r.allocated, 0);
  return total > 0 ? Math.round((allocated / total) * 100) : 0;
}

export function getResourceCapacity() {
  const total = mockResources.reduce((s, r) => s + r.availability, 0);
  const allocated = mockResources.reduce((s, r) => s + r.allocated, 0);
  return { total, allocated, available: total - allocated };
}

export function getOverallocatedResources() {
  return mockResources.filter((r) => r.allocated > r.availability).slice(0, 10);
}

export function getResourceAvailability() {
  return mockResources.filter((r) => r.availability - r.allocated > 0).slice(0, 10);
}

export function getInternalVsOutsourced() {
  return { internal: mockResources.filter((r) => r.type === "Internal").length, outsourced: mockResources.filter((r) => r.type === "Outsourced").length };
}

export function getPortfolioBudgetSummary() {
  return mockPortfolios.map((pf) => {
    const projects = mockProjects.filter((p) => p.portfolio === pf.id);
    return { id: pf.id, name: pf.name, budget: projects.reduce((s, p) => s + p.budget, 0), actual: projects.reduce((s, p) => s + p.actualCost, 0), forecast: projects.reduce((s, p) => s + p.forecast, 0), projectCount: projects.length };
  });
}

export function getTopRisks(limit = 10) {
  return [...mockRisks].sort((a, b) => b.score - a.score).slice(0, limit);
}

export function getPendingApprovals() {
  return mockApprovals.filter((a) => a.status === "Pending").slice(0, 10);
}

export function getUpcomingMeetings(limit = 5) {
  return mockMeetings.filter((m) => m.status === "Scheduled").slice(0, limit);
}

export function getUnreadNotifications() {
  return mockNotifications.filter((n) => !n.read).slice(0, 10);
}

export function getMeetingActions() {
  return mockMeetingActions.filter((a) => a.status === "Open").slice(0, 10);
}

export function getTasksByStatus() {
  const counts: Record<string, number> = {};
  mockTasks.forEach((t) => { counts[t.status] = (counts[t.status] || 0) + 1; });
  return counts;
}

export function getRiskTrend() {
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  return months.map((m, i) => ({
    month: m,
    new: Math.floor(Math.random() * 5) + 1,
    closed: Math.floor(Math.random() * 4),
    critical: Math.floor(Math.random() * 3),
  }));
}

export function getJiraSyncStatus() {
  return { status: "Connected", lastSync: "2 minutes ago", itemsSynced: 142, errors: 0 };
}

export function getMsProjectImportStatus() {
  return { status: "Connected", lastSync: "15 minutes ago", importsToday: 3, pending: 1 };
}

export function getIntegrationHealthList() {
  return [
    { name: "Jira Sync", status: "Connected" as const, lastSync: "2 min ago" },
    { name: "Microsoft Project", status: "Connected" as const, lastSync: "15 min ago" },
    { name: "ServiceNow", status: "Disconnected" as const, lastSync: "2 hours ago" },
    { name: "Azure DevOps", status: "Connected" as const, lastSync: "5 min ago" },
  ];
}

export function getAiRecommendations() {
  return [
    { title: "Resource Reallocation", description: "Reallocate 2 developers from Core Banking to Digital Lending Platform to mitigate schedule risk.", priority: "High" as const },
    { title: "Budget Optimisation", description: "ERP Cloud Migration shows 6.9% favourable variance. Consider releasing contingency reserve.", priority: "Medium" as const },
    { title: "Risk Mitigation", description: "AI Platform risk score trending up. Consider adding ML validation resources.", priority: "High" as const },
    { title: "Schedule Recovery", description: "Omni-Channel Hub can recover 5% variance by parallelising SIT and UAT.", priority: "Medium" as const },
  ];
}

export function getDelayedPredictions() {
  return [
    { project: "ESG Reporting Framework", probability: 92, reason: "35% variance, incomplete requirements" },
    { project: "HR & Payroll Transformation", probability: 85, reason: "17.9% cost overrun, data quality issues" },
    { project: "Open Banking Gateway", probability: 78, reason: "22% schedule variance, scope creep" },
    { project: "Identity & Access Management", probability: 72, reason: "20% delay, vendor integration complexity" },
  ];
}

export function getProjectsByPriority() {
  const counts: Record<string, number> = {};
  mockProjects.forEach((p) => { counts[p.priority] = (counts[p.priority] || 0) + 1; });
  return counts;
}

export function getMyTimesheetStatus() {
  return { status: "Submitted", weekEnding: "2026-08-01", totalHours: 37.5, approvedHours: 35, pendingHours: 2.5 };
}

export function getHoursThisWeek() {
  return { total: 37.5, billable: 32, nonBillable: 5.5, target: 37.5 };
}

export function getMissingTimesheets() {
  return [
    { name: "Alice Anderson", days: 3, department: "Engineering" },
    { name: "Bob Baker", days: 5, department: "Digital" },
    { name: "Charlie Clark", days: 2, department: "Infrastructure" },
    { name: "Diana Davis", days: 1, department: "Security" },
    { name: "Edward Evans", days: 4, department: "Data" },
  ];
}

export function getTimesheetApprovalInbox() {
  return [
    { id: "ts-1", name: "Raj Kapoor", weekEnding: "2026-07-25", hours: 40, status: "Pending" as const },
    { id: "ts-2", name: "Sophie Turner", weekEnding: "2026-07-25", hours: 38, status: "Pending" as const },
    { id: "ts-3", name: "Maria Garcia", weekEnding: "2026-07-25", hours: 35, status: "Pending" as const },
    { id: "ts-4", name: "James Wilson", weekEnding: "2026-07-25", hours: 42, status: "Pending" as const },
    { id: "ts-5", name: "Robert Davies", weekEnding: "2026-07-25", hours: 37, status: "Pending" as const },
  ];
}

export function getPlannedVsActual() {
  return [
    { project: "Core Banking Modernisation", planned: 1200, actual: 1150 },
    { project: "ERP Cloud Migration", planned: 800, actual: 780 },
    { project: "Omni-Channel Customer Hub", planned: 600, actual: 650 },
    { project: "Digital Lending Platform", planned: 400, actual: 380 },
    { project: "Data Centre Consolidation", planned: 300, actual: 290 },
  ];
}

export function getCostVariance() {
  return [
    { project: "Core Banking Modernisation", variance: 4.0, status: "favorable" as const },
    { project: "ERP Cloud Migration", variance: 6.9, status: "favorable" as const },
    { project: "Omni-Channel Customer Hub", variance: -6.8, status: "unfavorable" as const },
    { project: "Regulatory Compliance Suite", variance: -9.0, status: "unfavorable" as const },
    { project: "HR & Payroll Transformation", variance: -17.9, status: "unfavorable" as const },
  ];
}

export function getRiskHeatMap() {
  return [
    { category: "Technical", probability: 4, impact: 5, count: 3 },
    { category: "Regulatory", probability: 3, impact: 5, count: 2 },
    { category: "Security", probability: 3, impact: 5, count: 1 },
    { category: "Data", probability: 5, impact: 4, count: 1 },
    { category: "Operational", probability: 3, impact: 4, count: 2 },
    { category: "Market", probability: 4, impact: 5, count: 1 },
    { category: "Vendor", probability: 2, impact: 4, count: 1 },
    { category: "Supply Chain", probability: 4, impact: 3, count: 1 },
  ];
}
