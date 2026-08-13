import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { getJiraSyncStatus, getMsProjectImportStatus, getIntegrationHealthList } from "@/lib/dashboard-mock-data";

function JiraSyncStatusWidget({ config }: { config: WidgetConfig }) {
  const sync = getJiraSyncStatus();
  return (
    <div className="flex flex-col items-center justify-center h-full gap-2">
      <div className="flex items-center gap-2">
        <span className={`h-3 w-3 rounded-full ${sync.status === "Connected" ? "bg-success" : "bg-destructive"}`} />
        <span className="text-lg font-semibold">{sync.status}</span>
      </div>
      <div className="text-xs text-muted-foreground text-center">
        <p>Last sync: {sync.lastSync}</p>
        <p>{sync.itemsSynced} items synced</p>
        {sync.errors > 0 && <p className="text-destructive">{sync.errors} errors</p>}
      </div>
      <span className="text-[9px] text-muted-foreground/40 mt-1">Mock data</span>
    </div>
  );
}

function MsProjectImportWidget({ config }: { config: WidgetConfig }) {
  const imp = getMsProjectImportStatus();
  return (
    <div className="flex flex-col items-center justify-center h-full gap-2">
      <div className="flex items-center gap-2">
        <span className={`h-3 w-3 rounded-full ${imp.status === "Connected" ? "bg-success" : "bg-destructive"}`} />
        <span className="text-lg font-semibold">{imp.status}</span>
      </div>
      <div className="text-xs text-muted-foreground text-center">
        <p>Last import: {imp.lastSync}</p>
        <p>{imp.importsToday} imports today</p>
        {imp.pending > 0 && <p className="text-warning">{imp.pending} pending</p>}
      </div>
      <span className="text-[9px] text-muted-foreground/40 mt-1">Mock data</span>
    </div>
  );
}

function IntegrationHealthWidget({ config }: { config: WidgetConfig }) {
  const integrations = getIntegrationHealthList();
  return (
    <div className="space-y-1 h-full flex flex-col justify-center px-2">
      {integrations.map((int) => (
        <div key={int.name} className="flex items-center justify-between px-2 py-1.5 rounded-lg text-xs">
          <span className="font-medium">{int.name}</span>
          <div className="flex items-center gap-2">
            <span className="text-[10px] text-muted-foreground">{int.lastSync}</span>
            <span className={`flex items-center gap-1 text-[10px] ${int.status === "Connected" ? "text-success" : "text-destructive"}`}>
              <span className={`h-1.5 w-1.5 rounded-full ${int.status === "Connected" ? "bg-success" : "bg-destructive"}`} />
              {int.status}
            </span>
          </div>
        </div>
      ))}
      <span className="text-[9px] text-muted-foreground/40 text-center mt-1">Mock data</span>
    </div>
  );
}

registerWidget("jira-sync", {
  name: "Jira Sync Status", description: "Jira integration connection and sync status", category: "Integrations", icon: "Wifi",
  supportedPOVs: ["PMO Administrator", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle],
  dataSourceLabel: "Mock data",
}, JiraSyncStatusWidget);

registerWidget("ms-project-import", {
  name: "Microsoft Project Import Status", description: "Microsoft Project import connection status", category: "Integrations", icon: "FileSpreadsheet",
  supportedPOVs: ["PMO Administrator", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle],
  dataSourceLabel: "Mock data",
}, MsProjectImportWidget);

registerWidget("integration-health", {
  name: "Integration Health", description: "Overall health of all external integrations", category: "Integrations", icon: "Activity",
  supportedPOVs: ["PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle],
  dataSourceLabel: "Mock data",
}, IntegrationHealthWidget);
