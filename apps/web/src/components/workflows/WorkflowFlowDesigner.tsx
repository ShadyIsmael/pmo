"use client";

import * as React from "react";
import {
  Background,
  Connection,
  Controls,
  Edge,
  Handle,
  MiniMap,
  Node,
  NodeProps,
  Position,
  ReactFlow,
  type ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { Focus, LayoutDashboard, Link2, MousePointer2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { WorkflowState, WorkflowTransition, WorkflowVersionDetails } from "@/lib/api/workflows";

type DesignerNodeData = {
  state: WorkflowState;
  locale: string;
  onSelect: (_id: string) => void;
};

type DesignerNode = Node<DesignerNodeData>;

type Props = {
  version: WorkflowVersionDetails;
  locale: string;
  editable: boolean;
  onSelectState: (_id: string) => void;
  onSelectTransition: (_id: string) => void;
  onConnectStates: (_fromStateId: string, _toStateId: string) => Promise<void>;
};

const COLUMN_GAP = 300;
const ROW_GAP = 170;

function stateLabel(state: WorkflowState, locale: string) {
  return locale === "ar" ? state.nameAr : state.nameEn;
}

function stateNode({ data }: NodeProps<DesignerNode>) {
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

const nodeTypes = { workflowState: stateNode };

function initialPosition(index: number) {
  return { x: (index % 4) * COLUMN_GAP, y: Math.floor(index / 4) * ROW_GAP };
}

function buildNodes(versionId: string, states: WorkflowState[], locale: string, onSelect: (_id: string) => void): Node<DesignerNodeData>[] {
  const storageKey = `nexus-workflow-layout:${versionId}`;
  let saved: Record<string, { x: number; y: number }> = {};
  if (typeof window !== "undefined") {
    try { saved = JSON.parse(window.localStorage.getItem(storageKey) || "{}"); } catch { saved = {}; }
  }
  return states.map((state, index) => ({
    id: state.id,
    type: "workflowState",
    position: saved[state.id] ?? initialPosition(index),
    data: { state, locale, onSelect },
    draggable: true,
  }));
}

function buildEdges(transitions: WorkflowTransition[]): Edge[] {
  return transitions.map(transition => ({
    id: transition.id,
    source: transition.fromStateId,
    target: transition.toStateId,
    label: transition.code,
    type: "smoothstep",
    animated: false,
    markerEnd: { type: "arrowclosed" as const },
    style: { strokeWidth: 1.5 },
    labelStyle: { fontSize: 10, fontWeight: 600 },
  }));
}

function autoLayout(states: WorkflowState[], locale: string, onSelect: (_id: string) => void) {
  return states.map((state, index) => ({
    id: state.id,
    type: "workflowState",
    position: initialPosition(index),
    data: { state, locale, onSelect },
    draggable: true,
  }));
}

export function WorkflowFlowDesigner({ version, locale, editable, onSelectState, onSelectTransition, onConnectStates }: Props) {
  const [connectMode, setConnectMode] = React.useState(false);
  const [pendingSource, setPendingSource] = React.useState<string | null>(null);
  const onSelectStateRef = React.useRef(onSelectState);
  onSelectStateRef.current = onSelectState;
  const selectState = React.useCallback((id: string) => onSelectStateRef.current(id), []);
  const [nodes, setNodes, onNodesChange] = useNodesState(buildNodes(version.id, version.states, locale, selectState));
  const [edges, setEdges, onEdgesChange] = useEdgesState(buildEdges(version.transitions));
  const reactFlow = React.useRef<ReactFlowInstance<DesignerNode, Edge> | null>(null);

  React.useEffect(() => {
    setNodes(buildNodes(version.id, version.states, locale, selectState));
    setEdges(buildEdges(version.transitions));
  }, [version.id, version.states, version.transitions, locale, selectState, setEdges, setNodes]);

  const persistLayout = React.useCallback((nextNodes: Node[]) => {
    if (typeof window === "undefined") return;
    const positions = Object.fromEntries(nextNodes.map(node => [node.id, node.position]));
    window.localStorage.setItem(`nexus-workflow-layout:${version.id}`, JSON.stringify(positions));
  }, [version.id]);

  React.useEffect(() => {
    persistLayout(nodes);
  }, [nodes, persistLayout]);

  const handleConnect = React.useCallback(async (connection: Connection) => {
    if (!editable || !connection.source || !connection.target || connection.source === connection.target) return;
    if (edges.some(edge => edge.source === connection.source && edge.target === connection.target)) return;
    await onConnectStates(connection.source, connection.target);
    setConnectMode(false);
    setPendingSource(null);
  }, [editable, edges, onConnectStates]);

  const resetLayout = () => {
    const next = autoLayout(version.states, locale, selectState);
    setNodes(next);
  };

  return (
    <Card className="overflow-hidden">
      <CardHeader className="border-b bg-muted/20 py-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle className="text-base">{locale === "ar" ? "مصمم مسار العمل" : "Workflow Designer"}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{locale === "ar" ? "اسحب الحالات، حرّك العرض، ووصل الحالات بصريًا." : "Drag states, pan the canvas, and connect compatible states visually."}</p>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Button type="button" size="sm" variant={connectMode ? "default" : "outline"} disabled={!editable} onClick={() => { setConnectMode(value => !value); setPendingSource(null); }}><Link2 className="me-1.5 h-3.5 w-3.5" />{locale === "ar" ? "وصل الحالات" : "Connect states"}</Button>
            <Button type="button" size="icon" variant="outline" title="Auto layout" onClick={resetLayout}><LayoutDashboard className="h-3.5 w-3.5" /></Button>
            <Button type="button" size="icon" variant="outline" title="Fit view" onClick={() => reactFlow.current?.fitView({ padding: 0.2, duration: 250 })}><Focus className="h-3.5 w-3.5" /></Button>
            <Button type="button" size="icon" variant="outline" title="Reset layout" onClick={() => { resetLayout(); reactFlow.current?.fitView({ padding: 0.2, duration: 250 }); }}><RefreshCw className="h-3.5 w-3.5" /></Button>
          </div>
        </div>
        {connectMode ? <p className="mt-2 flex items-center gap-1 text-xs text-primary"><MousePointer2 className="h-3.5 w-3.5" />{locale === "ar" ? "اسحب من نقطة الخروج إلى الحالة المستهدفة." : "Drag from a source handle to the target state."}</p> : null}
      </CardHeader>
      <CardContent className="p-0">
        <div className="h-[560px] w-full bg-background [&_.react-flow__attribution]:hidden">
            <ReactFlow<DesignerNode, Edge>
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={(connection) => void handleConnect(connection)}
            onNodeClick={(_event, node) => {
              if (connectMode && !pendingSource) { setPendingSource(node.id); return; }
              onSelectState(node.id);
            }}
            onEdgeClick={(_event, edge) => onSelectTransition(edge.id)}
            onInit={(instance) => { reactFlow.current = instance; instance.fitView({ padding: 0.2 }); }}
            fitView
            nodesDraggable
            nodesConnectable={editable && connectMode}
            elementsSelectable
            proOptions={{ hideAttribution: true }}
          >
            <Background gap={24} size={1} />
            <Controls showInteractive={false} />
            <MiniMap pannable zoomable nodeColor={(node) => { const data = node.data as Partial<DesignerNodeData>; return data.state?.isInitial ? "#10b981" : data.state?.isFinal ? "#f59e0b" : "#64748b"; }} />
          </ReactFlow>
        </div>
      </CardContent>
    </Card>
  );
}
