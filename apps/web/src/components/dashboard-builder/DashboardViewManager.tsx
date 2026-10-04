"use client";

import * as React from "react";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogBody, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { getLookupTextClass } from "@/lib/status-presentation";
import { Plus, Pencil, Check, X, Copy, Trash2, Star, Lock, Users } from "lucide-react";
import type { DashboardAccess, DashboardStartMode, DashboardType, DashboardView, PointOfView } from "./dashboard-types";

interface CreateDashboardInput {
  name: string;
  type: DashboardType;
  startMode: DashboardStartMode;
  sourceViewId?: string;
}

interface DashboardViewManagerProps {
  open: boolean;
  onClose: () => void;
  pov: PointOfView;
  views: DashboardView[];
  currentViewId: string;
  onSwitch: (id: string) => void;
  onCreate: (input: CreateDashboardInput) => void;
  onRename: (id: string, name: string) => void;
  onDuplicate: (id: string) => void;
  onDelete: (id: string) => void;
  onSetDefault: (id: string) => void;
  onUpdateSharing: (id: string, type: DashboardType, access: DashboardAccess) => void;
}

const accessLabel: Record<DashboardAccess, string> = {
  view: "View only",
  duplicate: "Can duplicate",
  edit: "Can edit",
  "manage-sharing": "Manage sharing",
};

export function DashboardViewManager({
  open, onClose, pov, views, currentViewId, onSwitch, onCreate, onRename,
  onDuplicate, onDelete, onSetDefault, onUpdateSharing,
}: DashboardViewManagerProps) {
  const [newName, setNewName] = React.useState("");
  const [startMode, setStartMode] = React.useState<DashboardStartMode>("blank");
  const [type, setType] = React.useState<DashboardType>("personal");
  const [sourceViewId, setSourceViewId] = React.useState("");
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [editValue, setEditValue] = React.useState("");

  const handleCreate = () => {
    if (!newName.trim()) return;
    onCreate({ name: newName.trim(), type, startMode, sourceViewId: startMode === "duplicate" ? sourceViewId : undefined });
    setNewName("");
  };

  return (
    <Dialog open={open} onOpenChange={(value) => !value && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="text-sm">Manage {pov} Dashboards</DialogTitle>
          <DialogDescription className="text-xs">Create personal dashboards or manage dashboards shared in this point of view.</DialogDescription>
        </DialogHeader>

        <DialogBody>
        <div className="grid gap-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-4">
          <div className="sm:col-span-2">
            <Label className="text-[10px]">Dashboard name</Label>
            <Input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="e.g. Weekly project review" className="mt-1 h-8 text-xs" />
          </div>
          <div>
            <Label className="text-[10px]">Start from</Label>
            <Select value={startMode} onValueChange={(value) => setStartMode(value as DashboardStartMode)}>
              <SelectTrigger className="mt-1 h-8 text-xs"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="blank">Blank</SelectItem>
                <SelectItem value="template">Default template</SelectItem>
                <SelectItem value="duplicate">Duplicate existing</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label className="text-[10px]">Type</Label>
            <Select value={type} onValueChange={(value) => setType(value as DashboardType)}>
              <SelectTrigger className="mt-1 h-8 text-xs"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="personal">Personal</SelectItem><SelectItem value="shared">Shared</SelectItem></SelectContent>
            </Select>
          </div>
          {startMode === "duplicate" && (
            <div className="sm:col-span-3">
              <Label className="text-[10px]">Dashboard to duplicate</Label>
              <Select value={sourceViewId} onValueChange={setSourceViewId}>
                <SelectTrigger className="mt-1 h-8 text-xs"><SelectValue placeholder="Select dashboard" /></SelectTrigger>
                <SelectContent>{views.map((view) => <SelectItem key={view.id} value={view.id}>{view.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          )}
          <div className={startMode === "duplicate" ? "flex items-end" : "sm:col-span-4 flex justify-end"}>
            <Button size="sm" className="h-8 text-xs" onClick={handleCreate} disabled={!newName.trim() || (startMode === "duplicate" && !sourceViewId)}>
              <Plus className="mr-1 h-3.5 w-3.5" />Create Dashboard
            </Button>
          </div>
        </div>

        <ScrollArea className="max-h-[380px]">
          <div className="space-y-2 pr-3">
            {views.map((view) => {
              const isCurrent = view.id === currentViewId;
              const isEditing = editingId === view.id;
              const canEdit = view.access === "edit" || view.access === "manage-sharing";
              const canDuplicate = view.access !== "view";
              const canManage = view.access === "manage-sharing";
              return (
                <div key={view.id} className={`rounded-lg border p-2.5 ${isCurrent ? "border-primary/40 bg-primary/5" : "border-border/40"}`}>
                  <div className="flex items-center gap-2">
                    {isEditing ? (
                      <Input value={editValue} onChange={(e) => setEditValue(e.target.value)} className="h-7 flex-1 text-xs" autoFocus />
                    ) : (
                      <button className="min-w-0 flex-1 text-left" onClick={() => { onSwitch(view.id); onClose(); }}>
                        <span className="flex items-center gap-1.5 text-xs font-medium">
                          <span className="truncate">{view.name}</span>
                          {view.isDefault && <Star className="h-3 w-3 shrink-0 fill-warning text-warning" />}
                        </span>
                        <span className="mt-0.5 block text-[9px] text-muted-foreground">{view.widgets.length} widgets · Owner: {view.ownerName}{isCurrent ? " · Current" : ""}</span>
                      </button>
                    )}
                    <span className={`inline-flex h-5 items-center gap-1 text-[10px] font-medium ${getLookupTextClass(view.type)}`}>{view.type === "shared" ? <Users className="h-2.5 w-2.5" /> : <Lock className="h-2.5 w-2.5" />}{view.type}</span>
                    {isEditing ? (
                      <>
                        <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => { if (editValue.trim()) onRename(view.id, editValue.trim()); setEditingId(null); }}><Check className="h-3 w-3" /></Button>
                        <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => setEditingId(null)}><X className="h-3 w-3" /></Button>
                      </>
                    ) : (
                      <>
                        {canEdit && <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => { setEditingId(view.id); setEditValue(view.name); }}><Pencil className="h-3 w-3" /></Button>}
                        {canDuplicate && <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => onDuplicate(view.id)}><Copy className="h-3 w-3" /></Button>}
                        {canEdit && !view.isDefault && <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => onSetDefault(view.id)}><Star className="h-3 w-3" /></Button>}
                        {canEdit && views.length > 1 && <Button variant="ghost" size="icon" className="h-6 w-6 text-destructive" onClick={() => onDelete(view.id)}><Trash2 className="h-3 w-3" /></Button>}
                      </>
                    )}
                  </div>
                  {view.type === "shared" && (
                    <div className="mt-2 flex items-center justify-between border-t pt-2">
                      <span className="text-[9px] text-muted-foreground">Shared dashboard access</span>
                      <Select value={view.access} disabled={!canManage} onValueChange={(value) => onUpdateSharing(view.id, "shared", value as DashboardAccess)}>
                        <SelectTrigger className="h-7 w-36 text-[10px]"><SelectValue /></SelectTrigger>
                        <SelectContent>{Object.entries(accessLabel).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
                      </Select>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </ScrollArea>
        </DialogBody>
      </DialogContent>
    </Dialog>
  );
}
