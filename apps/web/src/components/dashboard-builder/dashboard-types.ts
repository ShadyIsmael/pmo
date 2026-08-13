export type PointOfView =
  | "Executive"
  | "Project Manager"
  | "Portfolio Manager"
  | "Team Member"
  | "Resource Manager"
  | "Finance Controller"
  | "PMO Administrator";

export type WidgetCategory =
  | "Overview"
  | "Projects"
  | "Portfolios"
  | "My Work"
  | "Schedule"
  | "Resources"
  | "Timesheets"
  | "Costs"
  | "Risks"
  | "Meetings"
  | "Approvals"
  | "Notifications"
  | "Integrations"
  | "AI";

export type DisplayDensity = "compact" | "normal" | "comfortable";
export type ChartType = "bar" | "line" | "pie" | "donut" | "area" | "number" | "list";
export type DashboardType = "personal" | "shared";
export type DashboardAccess = "view" | "duplicate" | "edit" | "manage-sharing";
export type DashboardStartMode = "blank" | "template" | "duplicate";

export interface WidgetPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface WidgetConfig {
  customTitle?: string;
  portfolio?: string;
  program?: string;
  project?: string;
  dateRange?: "7d" | "30d" | "90d" | "ytd" | "1y";
  health?: string[];
  status?: string[];
  owner?: string;
  resource?: string;
  department?: string;
  timesheetStatus?: string;
  billableStatus?: string;
  maxRecords?: number;
  chartType?: ChartType;
  groupBy?: string;
  sortBy?: string;
  showLegend?: boolean;
  showTrend?: boolean;
  includeCompleted?: boolean;
  refreshInterval?: number;
  displayDensity?: DisplayDensity;
}

export interface WidgetDefinition {
  type: string;
  name: string;
  description: string;
  category: WidgetCategory;
  icon: string;
  supportedPOVs: PointOfView[];
  requiredPermission?: string;
  requiredPlugin?: string;
  minSize: WidgetPosition;
  defaultSize: WidgetPosition;
  maxSize: WidgetPosition;
  defaultConfig: WidgetConfig;
  configFields: WidgetConfigField[];
  dataSourceLabel?: string;
}

export interface WidgetConfigField {
  key: string;
  label: string;
  type: "text" | "number" | "select" | "boolean" | "multi-select";
  options?: { label: string; value: string }[];
  defaultValue?: string | number | boolean;
  placeholder?: string;
}

export interface WidgetInstance {
  id: string;
  type: string;
  title: string;
  config: WidgetConfig;
  position: WidgetPosition;
  collapsed: boolean;
  lastUpdated: number | null;
  loading: boolean;
  error: string | null;
}

export interface DashboardView {
  id: string;
  name: string;
  description?: string;
  pov: PointOfView;
  ownerId: string;
  ownerName: string;
  type: DashboardType;
  access: DashboardAccess;
  sharedWith: string[];
  widgets: WidgetInstance[];
  filters: GlobalFilters;
  isDefault: boolean;
  createdAt: number;
  updatedAt: number;
}

export interface GlobalFilters {
  dateRange: string;
  portfolio: string | null;
  program: string | null;
  project: string | null;
}

export interface DashboardState {
  currentViewId: string;
  views: DashboardView[];
  pov: PointOfView;
  lastViewByPOV: Partial<Record<PointOfView, string>>;
  editMode: boolean;
  lastUpdated: number | null;
}
