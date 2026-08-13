"use client";

import * as React from "react";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ScrollArea } from "@/components/ui/scroll-area";
import { getWidgetDefinition } from "./widget-registry";
import type { WidgetConfig, WidgetConfigField } from "./dashboard-types";

interface WidgetSettingsProps {
  open: boolean;
  onClose: () => void;
  widgetType: string;
  widgetTitle: string;
  config: WidgetConfig;
  onSave: (config: WidgetConfig) => void;
}

function FieldEditor({ field, value, onChange }: { field: WidgetConfigField; value: unknown; onChange: (key: string, value: unknown) => void }) {
  const val = value ?? field.defaultValue ?? "";
  switch (field.type) {
    case "text":
      return (
        <div className="space-y-1">
          <Label className="text-[11px]">{field.label}</Label>
          <Input className="h-7 text-xs" value={String(val)} onChange={(e) => onChange(field.key, e.target.value)} placeholder={field.placeholder} />
        </div>
      );
    case "number":
      return (
        <div className="space-y-1">
          <Label className="text-[11px]">{field.label}</Label>
          <Input className="h-7 text-xs" type="number" value={Number(val)} onChange={(e) => onChange(field.key, parseInt(e.target.value) || 0)} />
        </div>
      );
    case "select":
      return (
        <div className="space-y-1">
          <Label className="text-[11px]">{field.label}</Label>
          <Select value={String(val)} onValueChange={(v) => onChange(field.key, v)}>
            <SelectTrigger className="h-7 text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {field.options?.map((o) => (
                <SelectItem key={o.value} value={o.value} className="text-xs">{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      );
    case "boolean":
      return (
        <div className="flex items-center justify-between">
          <Label className="text-[11px] cursor-pointer">{field.label}</Label>
          <Switch checked={Boolean(val)} onCheckedChange={(v) => onChange(field.key, v)} />
        </div>
      );
  }
}

export function WidgetSettings({ open, onClose, widgetType, widgetTitle, config, onSave }: WidgetSettingsProps) {
  const def = React.useMemo(() => getWidgetDefinition(widgetType), [widgetType]);
  const [local, setLocal] = React.useState<WidgetConfig>({ ...config });

  React.useEffect(() => { setLocal({ ...config }); }, [config, open]);

  if (!def) return null;

  const handleChange = (key: string, value: unknown) => setLocal((prev) => ({ ...prev, [key]: value }));

  return (
    <SideSheet
      open={open}
      onOpenChange={(nextOpen) => { if (!nextOpen) onClose(); }}
      title={widgetTitle}
      description="Configure widget display and data"
      size="sm"
      titleClassName="text-sm"
      descriptionClassName="text-xs"
    >
      <SideSheetBody className="flex flex-col overflow-hidden">
        <ScrollArea className="min-h-0 flex-1 pe-2">
          <div className="space-y-3">
            {def.configFields.map((field) => (
              <FieldEditor key={field.key} field={field} value={local[field.key as keyof WidgetConfig]} onChange={handleChange} />
            ))}
            {def.configFields.length === 0 && (
              <p className="text-xs text-muted-foreground text-center py-4">No configurable settings for this widget</p>
            )}
          </div>
        </ScrollArea>
      </SideSheetBody>
      <SideSheetFooter>
          <Button variant="outline" size="sm" className="flex-1 h-8 text-xs" onClick={onClose}>Cancel</Button>
          <Button size="sm" className="flex-1 h-8 text-xs" onClick={() => { onSave(local); onClose(); }}>Apply</Button>
      </SideSheetFooter>
    </SideSheet>
  );
}
