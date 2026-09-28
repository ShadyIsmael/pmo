import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { useDashboardLabels } from "../dashboard-data";

function UnavailableIntegration({ config: _config }: { config: WidgetConfig }) { const { t } = useDashboardLabels(); return <div className="flex min-h-32 items-center justify-center px-4 text-center text-xs text-muted-foreground">{t("sourceUnavailable")}</div>; }
const definition = { minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 }, defaultConfig: {}, configFields: [f.customTitle], dataSourceLabel: "Unavailable" };
for (const [type, name] of [["jira-sync", "Jira Sync"], ["ms-project-import", "Microsoft Project Import"], ["integration-health", "Integration Health"]] as const) registerWidget(type, { ...definition, name, description: "Integration status is not available in the current read model", category: "Integrations", icon: "Plug", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"] }, UnavailableIntegration);
