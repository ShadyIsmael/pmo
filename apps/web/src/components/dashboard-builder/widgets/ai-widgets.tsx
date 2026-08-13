import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { getAiRecommendations, getDelayedPredictions } from "@/lib/dashboard-mock-data";

function AiRecommendationsWidget({ config }: { config: WidgetConfig }) {
  const recs = getAiRecommendations();
  return (
    <div className="space-y-1 h-full overflow-y-auto">
      {recs.map((r, i) => (
        <div key={i} className="flex items-start gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full ${r.priority === "High" ? "bg-destructive/10 text-destructive" : "bg-primary/10 text-primary"}`}>
            <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-medium truncate">{r.title}</span>
              <span className={`shrink-0 px-1 py-0.5 rounded text-[9px] font-medium ${r.priority === "High" ? "bg-destructive/10 text-destructive" : "bg-warning/10 text-warning"}`}>{r.priority}</span>
            </div>
            <p className="text-[10px] text-muted-foreground mt-0.5 line-clamp-2">{r.description}</p>
          </div>
        </div>
      ))}
      <span className="text-[9px] text-muted-foreground/40 block text-center pt-1">Mock AI data {"\u2014"} not real predictions</span>
    </div>
  );
}

function DelayedPredictionsWidget({ config }: { config: WidgetConfig }) {
  const predictions = getDelayedPredictions();
  return (
    <div className="space-y-1 h-full overflow-y-auto">
      {predictions.map((p, i) => (
        <div key={i} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 cursor-pointer transition-colors text-xs">
          <div className="flex-1 min-w-0">
            <div className="font-medium truncate">{p.project}</div>
            <div className="text-[10px] text-muted-foreground">{p.reason}</div>
          </div>
          <span className={`shrink-0 text-sm font-bold ${p.probability >= 80 ? "text-destructive" : p.probability >= 60 ? "text-warning" : "text-success"}`}>{p.probability}%</span>
        </div>
      ))}
      <span className="text-[9px] text-muted-foreground/40 block text-center pt-1">Mock AI data {"\u2014"} not real predictions</span>
    </div>
  );
}

registerWidget("ai-recommendations", {
  name: "AI Recommendations", description: "AI-generated insights and suggested actions", category: "AI", icon: "Lightbulb",
  supportedPOVs: ["Executive", "Portfolio Manager", "Project Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords],
  dataSourceLabel: "Mock AI data",
}, AiRecommendationsWidget);

registerWidget("delayed-predictions", {
  name: "Likely Delays", description: "AI predictions of projects at risk of delay", category: "AI", icon: "TrendingDown",
  supportedPOVs: ["Executive", "Portfolio Manager", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords],
  dataSourceLabel: "Mock AI data",
}, DelayedPredictionsWidget);
