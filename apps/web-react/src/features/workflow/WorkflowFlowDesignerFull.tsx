import * as React from "react";
import {
  Background,
  Connection,
  Controls,
  Handle,
  MiniMap,
  Position,
  ReactFlow,
  type Edge,
  type Node,
  type NodeProps,
  type ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from "@xyflow/react";
import { Focus, LayoutDashboard, Link2, MousePointer2, RefreshCw } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import type { WorkflowState, WorkflowTransition, WorkflowVersionDetails } from "./api/workflowsFull";

type DesignerNodeData = {
  state: WorkflowState;
  locale: string;
  onSelect: (id: string) => void;
};

type DesignerNode = Node<DesignerNodeData>;

type Props = {
  version: WorkflowVersionDetails;
  locale: string;
  editable: boolean;
  labels: {
    title: string;
    hint: string;
    connect: string;
    autoLayout: string;
    fitView: string;
    resetLayout: string;
    connectHint: string;
    connectFailed: string;
    initial: string;
    terminal: string;
    normalState: string;
  };
  onSelectState: (id: string) => void;
  onSelectTransition: (id: string) => void;
  onConnectStates: (fromStateId: string, toStateId: string) => Promise<void>;
};

const COLUMN_GAP = 300;
const ROW_GAP = 170;

function stateLabel(state: WorkflowState, locale: string) {
  return locale === "ar" ? state.nameAr : state.nameEn;
}

function StateNode({ data }: NodeProps<DesignerNode>) {
  const { state, locale, onSelect } = data;
  return (
    <div
      className={`min-w-[220px] rounded-xl border-2 bg-card p-4 text-start shadow-sm transition hover:shadow-md ${state.isInitial ? "border-emerald-500" : state.isFinal ? "border-amber-500" : "border-border"}`}
      onClick={() => onSelect(state.id)}
    >
      <Handle type="target" position={Position.Left} className="!h-2.5 !w-2.5 !border-2 !border-background !bg-muted-foreground" />
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">{stateLabel(state, locale)}</p>
          <p className="mt-1 font-mono text-[10px] uppercase tracking-wide text-muted-foreground">{state.code}</p>
        </div>
        {state.isInitial ? <Badge status="Active">{locale === "ar" ? "بداية" : "Initial"}</Badge> : state.isFinal ? <Badge status="Done">{locale === "ar" ? "نهائية" : "Terminal"}</Badge> : null}
      </div>
      <p className="mt-5 text-[11px] text-muted-foreground">{state.description || (locale === "ar" ? "حالة عادية" : "Normal state")}</p>
      <Handle type="source" position={Position.Right} className="!h-2.5 !w-2.5 !border-2 !border-background !bg-primary" />
    </div>
  );
}

const nodeTypes = { workflowState: StateNode };

function initialPosition(index: number) {
  return { x: (index % 4) * COLUMN_GAP, y: Math.floor(index / 4) * ROW_GAP };
}

function readLayout(versionId: string) {
  if (typeof window === "undefined") return {};
  try {
    return JSON.parse(window.localStorage.getItem(`nexus-workflow-layout:${versionId}`) || "{}") as Record<string, { x: number; y: number }>;
  } catch {
    return {};
  }
}

function buildNodes(version: WorkflowVersionDetails, locale: string, onSelect: (id: string) => void): DesignerNode[] {
  const saved = readLayout(version.id);
  return version.states.map((state, index) => ({
    id: state.id,
    type: "workflowState",
    position: saved[state.id] ?? initialPosition(index),
    data: { state, locale, onSelect },
    draggable: true,
  }));
}

export function buildWorkflowEdges(transitions: WorkflowTransition[]): Edge[] {
  return transitions.map((transition) => ({
    id: transition.id,
    source: transition.fromStateId,
    target: transition.toStateId,
    label: transition.code,
    type: "smoothstep",
    markerEnd: { type: "arrowclosed" as const },
    style: { strokeWidth: 1.5 },
    labelStyle: { fontSize: 10, fontWeight: 600 },
  }));
}

export function WorkflowFlowDesignerFull({ version, locale, editable, labels, onSelectState, onSelectTransition, onConnectStates }: Props) {
  const onSelectStateRef = React.useRef(onSelectState);
  onSelectStateRef.current = onSelectState;
  const selectState = React.useCallback((id: string) => onSelectStateRef.current(id), []);
  const [connectMode, setConnectMode] = React.useState(false);
  const [connectError, setConnectError] = React.useState<string | null>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState<DesignerNode>(buildNodes(version, locale, selectState));
  const [edges, setEdges, onEdgesChange] = useEdgesState(buildWorkflowEdges(version.transitions));
  const reactFlow = React.useRef<ReactFlowInstance<DesignerNode, Edge> | null>(null);

  React.useEffect(() => {
    setNodes(buildNodes(version, locale, selectState));
    setEdges(buildWorkflowEdges(version.transitions));
  }, [locale, selectState, setEdges, setNodes, version]);

  React.useEffect(() => {
    if (typeof window === "undefined") return;
    const positions = Object.fromEntries(nodes.map((node) => [node.id, node.position]));
    window.localStorage.setItem(`nexus-workflow-layout:${version.id}`, JSON.stringify(positions));
  }, [nodes, version.id]);

  const handleConnect = React.useCallback(async (connection: Connection) => {
    if (!editable || !connectMode || !connection.source || !connection.target || connection.source === connection.target) return;
    if (edges.some((edge) => edge.source === connection.source && edge.target === connection.target)) return;
    setConnectError(null);
    try {
      await onConnectStates(connection.source, connection.target);
      setConnectMode(false);
    } catch {
      setConnectError(labels.connectFailed);
    }
  }, [connectMode, editable, edges, labels.connectFailed, onConnectStates]);

  const resetLayout = React.useCallback(() => {
    setNodes(buildNodes(version, locale, selectState).map((node, index) => ({ ...node, position: initialPosition(index) })));
  }, [locale, selectState, setNodes, version]);

  return (
    <Card className="overflow-hidden">
      <CardHeader className="border-b bg-muted/20 py-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle className="text-base">{labels.title}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{labels.hint}</p>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Button type="button" size="sm" variant={connectMode ? "default" : "outline"} disabled={!editable} onClick={() => { setConnectError(null); setConnectMode((value) => !value); }}><Link2 className="me-1.5 h-3.5 w-3.5" />{labels.connect}</Button>
            <Button type="button" size="icon" variant="outline" title={labels.autoLayout} onClick={resetLayout}><LayoutDashboard className="h-3.5 w-3.5" /></Button>
            <Button type="button" size="icon" variant="outline" title={labels.fitView} onClick={() => reactFlow.current?.fitView({ padding: 0.2, duration: 250 })}><Focus className="h-3.5 w-3.5" /></Button>
            <Button type="button" size="icon" variant="outline" title={labels.resetLayout} onClick={() => { resetLayout(); reactFlow.current?.fitView({ padding: 0.2, duration: 250 }); }}><RefreshCw className="h-3.5 w-3.5" /></Button>
          </div>
        </div>
        {connectMode ? <p className="mt-2 flex items-center gap-1 text-xs text-primary"><MousePointer2 className="h-3.5 w-3.5" />{labels.connectHint}</p> : null}
        {connectError ? <p role="alert" className="mt-2 text-xs text-destructive">{connectError}</p> : null}
      </CardHeader>
      <CardContent className="p-0">
        <div className="h-[560px] w-full bg-background">
          <ReactFlow<DesignerNode, Edge>
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={(connection) => void handleConnect(connection)}
            onNodeClick={(_event, node) => { if (!connectMode) onSelectState(node.id); }}
            onEdgeClick={(_event, edge) => onSelectTransition(edge.id)}
            onInit={(instance) => { reactFlow.current = instance; instance.fitView({ padding: 0.2 }); }}
            fitView
            nodesDraggable
            nodesConnectable={editable && connectMode}
            elementsSelectable
            proOptions={{ hideAttribution: false }}
            className="[&_.react-flow__attribution]:block"
          >
            <Background gap={24} size={1} />
            <Controls showInteractive={false} />
            <MiniMap pannable zoomable nodeColor={(node) => { const data = node.data as Partial<DesignerNodeData>; return data.state?.isInitial ? "hsl(var(--success))" : data.state?.isFinal ? "hsl(var(--warning))" : "hsl(var(--muted-foreground))"; }} />
          </ReactFlow>
        </div>
      </CardContent>
    </Card>
  );
}
