import * as React from "react";
import { useParams, Link } from "react-router-dom";
import { ReactFlow, Background, Controls, MiniMap, Handle, Position, type Node, type Edge } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";
import { useI18n } from "@i18n/messages";
import { useWorkflowDefinition, useWorkflowVersion, useWorkflowMutations } from "./api/workflows";

function StateNode({ data }: { data: { label: string; terminal?: boolean } }) {
  return (
    <div className="min-w-36 rounded-lg border bg-card px-3 py-2 text-center shadow-sm">
      <Handle type="target" position={Position.Top} />
      <p className="text-sm font-semibold">{data.label}</p>
      {data.terminal ? <p className="text-[11px] text-muted-foreground">terminal</p> : null}
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
}

const nodeTypes = { workflowState: StateNode };

export function WorkflowDetailPage() {
  const { id, versionId } = useParams<{ id: string; versionId?: string }>();
  const { t } = useI18n();
  const definition = useWorkflowDefinition(id);
  const mutations = useWorkflowMutations();
  const [selectedVersionId, setSelectedVersion] = React.useState<string>("");
  const versions = definition.data?.versions ?? [];
  const activeVersionId = selectedVersionId || versionId || versions.find((v) => v.status === "Published")?.id || versions[0]?.id || "";
  // Visible-first: version graph loads only when a version is selected.
  const version = useWorkflowVersion(activeVersionId || undefined);
  const [selectedState, setSelectedState] = React.useState<string | null>(null);

  const { nodes, edges } = React.useMemo(() => {
    const states = version.data?.states ?? [];
    const transitions = version.data?.transitions ?? [];
    const nodes: Node[] = states.map((s, i) => ({
      id: s.id,
      type: "workflowState",
      position: { x: (i % 4) * 220, y: Math.floor(i / 4) * 140 },
      data: { label: s.name, terminal: s.isTerminal },
      draggable: true,
    }));
    const edges: Edge[] = transitions.map((tr) => ({ id: tr.id, source: tr.fromStateId, target: tr.toStateId, label: tr.name, type: "smoothstep" }));
    return { nodes, edges };
  }, [version.data]);

  if (definition.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (definition.isError || !definition.data) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  const def = definition.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title={def.name}
        description={`${def.code} · ${def.domain}`}
        actions={
          <div className="flex gap-2">
            <select className="input-field h-9 max-w-56" value={activeVersionId} onChange={(e) => setSelectedVersion(e.target.value)} aria-label={t("workflows.version")}>
              {versions.map((v) => <option key={v.id} value={v.id}>v{v.version} — {v.status}</option>)}
            </select>
            <Button size="sm" variant="outline" onClick={() => mutations.createDraft.mutate(def.id)} disabled={mutations.createDraft.isPending}>{t("workflows.newDraft")}</Button>
            {version.data && version.data.status === "Draft" ? (
              <>
                <Button size="sm" variant="outline" onClick={() => mutations.validate.mutate(version.data.id)} disabled={mutations.validate.isPending}>{t("workflows.validate")}</Button>
                <Button size="sm" onClick={() => mutations.publish.mutate(version.data.id)} disabled={mutations.publish.isPending}>{t("workflows.publish")}</Button>
              </>
            ) : null}
          </div>
        }
      />
      <Card>
        <CardHeader><CardTitle>{t("workflows.designer")}</CardTitle></CardHeader>
        <CardContent>
          {version.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : !version.data ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
            : (
              <div className="h-[520px] overflow-hidden rounded-lg border">
                <ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} onNodeClick={(_, n) => setSelectedState(n.id)} fitView panOnDrag zoomOnScroll>
                  <Background />
                  <Controls />
                  <MiniMap />
                </ReactFlow>
              </div>
            )}
          <p className="mt-2 text-xs text-muted-foreground">
            <Link to={activeVersionId ? `/workflows/${encodeURIComponent(def.id)}/versions/${encodeURIComponent(activeVersionId)}` : "#"} className="hover:underline">{t("workflows.openVersion")}</Link>
          </p>
        </CardContent>
      </Card>
      <SideSheet open={Boolean(selectedState)} onOpenChange={(o) => { if (!o) setSelectedState(null); }} title={t("workflows.stateDetails")}>
        <SideSheetBody>
          <p className="text-sm">{version.data?.states.find((s) => s.id === selectedState)?.name ?? selectedState}</p>
        </SideSheetBody>
      </SideSheet>
    </div>
  );
}
