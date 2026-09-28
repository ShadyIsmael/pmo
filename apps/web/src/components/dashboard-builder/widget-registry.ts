import type { WidgetConfig, WidgetDefinition, WidgetConfigField, PointOfView } from "./dashboard-types";
import type { ComponentType } from "react";

interface RegistryEntry {
  definition: WidgetDefinition;
  component: ComponentType<{ config: WidgetConfig }>;
}

const registry = new Map<string, RegistryEntry>();

export function registerWidget(
  type: string,
  def: Omit<WidgetDefinition, "type">,
  component: ComponentType<{ config: WidgetConfig }>,
) {
  registry.set(type, { definition: { ...def, type }, component });
}

export function getWidgetDefinition(type: string): WidgetDefinition | undefined {
  return registry.get(type)?.definition;
}

export function getWidgetComponent(type: string): ComponentType<{ config: WidgetConfig }> | undefined {
  return registry.get(type)?.component;
}

export function getAllDefinitions(): WidgetDefinition[] {
  return Array.from(registry.values()).map((e) => e.definition);
}

export function getDefinitionsByCategory(category: string): WidgetDefinition[] {
  return getAllDefinitions().filter((d) => d.category === category);
}

export function getDefinitionsByPOV(pov: string): WidgetDefinition[] {
  return getAllDefinitions().filter((d) =>
    d.supportedPOVs.includes(pov as PointOfView),
  );
}

function sel(label: string, value: string): { label: string; value: string } {
  return { label, value };
}

export function field(key: string, label: string, type: WidgetConfigField["type"], options?: { label: string; value: string }[], def?: string | number | boolean): WidgetConfigField {
  return { key, label, type, options, defaultValue: def };
}

export const commonFields = {
  customTitle: field("customTitle", "Custom Title", "text"),
  dateRange: field("dateRange", "Date Range", "select", [sel("Last 7 Days", "7d"), sel("Last 30 Days", "30d"), sel("Last 90 Days", "90d"), sel("Year to Date", "ytd"), sel("Last Year", "1y")], "30d"),
  maxRecords: field("maxRecords", "Max Records", "number", undefined, 10),
  showLegend: field("showLegend", "Show Legend", "boolean", undefined, true),
  showTrend: field("showTrend", "Show Trend", "boolean", undefined, false),
  includeCompleted: field("includeCompleted", "Include Completed", "boolean", undefined, false),
  portfolio: field("portfolio", "Portfolio", "select", [sel("All", "_all")]),
  program: field("program", "Program", "select", [sel("All", "_all")]),
  project: field("project", "Project", "select", [sel("All", "_all")]),
  chartType: field("chartType", "Chart Type", "select", [sel("Bar", "bar"), sel("Line", "line"), sel("Pie", "pie"), sel("Donut", "donut"), sel("Area", "area"), sel("Number", "number"), sel("List", "list")], "bar"),
  groupBy: field("groupBy", "Group By", "select", [sel("Status", "status"), sel("Health", "health"), sel("Priority", "priority"), sel("Portfolio", "portfolio"), sel("Program", "program"), sel("Department", "department")]),
  sortBy: field("sortBy", "Sort By", "select", [sel("Name", "name"), sel("Progress", "progress"), sel("Budget", "budget"), sel("Variance", "variance"), sel("Due Date", "dueDate"), sel("Priority", "priority")]),
  owner: field("owner", "Owner", "select", [sel("Any", "_all")]),
  resource: field("resource", "Resource", "select", [sel("Any", "_all")]),
  department: field("department", "Department", "select", [sel("All", "_all")]),
  timesheetStatus: field("timesheetStatus", "Timesheet Status", "select", [sel("All", "_all"), sel("Submitted", "submitted"), sel("Approved", "approved"), sel("Pending", "pending"), sel("Missing", "missing")]),
  billableStatus: field("billableStatus", "Billable Status", "select", [sel("All", "_all"), sel("Billable", "billable"), sel("Non-Billable", "non-billable")]),
  health: field("health", "Health", "multi-select", [sel("Green", "green"), sel("Amber", "amber"), sel("Red", "red")]),
  status: field("status", "Status", "multi-select", [sel("Active", "Active"), sel("At Risk", "At Risk"), sel("Delayed", "Delayed"), sel("On Hold", "On Hold"), sel("Completed", "Completed")]),
};

export { registry };
