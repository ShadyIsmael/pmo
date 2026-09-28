import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { useDashboardLabels } from "../dashboard-data";

function UnavailableAiWidget({ config: _config }: { config: WidgetConfig }) { const { t } = useDashboardLabels(); return <div className="flex min-h-32 items-center justify-center px-4 text-center text-xs text-muted-foreground">{t("sourceUnavailable")}</div>; }
const definition = { minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 }, defaultConfig: {}, configFields: [f.customTitle], dataSourceLabel: "Unavailable" };
for (const [type, name] of [["ai-recommendations", "AI Recommendations"], ["delayed-predictions", "Delay Predictions"]] as const) registerWidget(type, { ...definition, name, description: "No authoritative predictive source is connected", category: "AI", icon: "Sparkles", supportedPOVs: ["Executive", "Project Manager", "Program Manager", "Portfolio Manager" ] }, UnavailableAiWidget);
