"use client";

import * as React from "react";
import { SideSheet, SideSheetBody } from "@/components/ui/side-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { getAllDefinitions } from "./widget-registry";
import { usePermissions } from "@/lib/permissions";
import type { WidgetDefinition, WidgetCategory, PointOfView } from "./dashboard-types";
import { Search, Plus, Eye } from "lucide-react";

interface WidgetLibraryProps {
  open: boolean;
  onClose: () => void;
  onAddWidget: (type: string) => void;
  pov: PointOfView;
}

const categories: WidgetCategory[] = [
  "Overview", "Projects", "Portfolios", "My Work", "Schedule",
  "Resources", "Timesheets", "Costs", "Risks", "Meetings", "Approvals",
  "Notifications", "Integrations", "AI",
];

export function WidgetLibrary({ open, onClose, onAddWidget, pov }: WidgetLibraryProps) {
  const [search, setSearch] = React.useState("");
  const [category, setCategory] = React.useState<string>("All");
  const userPermissions = usePermissions();

  const definitions = React.useMemo(() => {
    let defs = getAllDefinitions().filter((d) => d.supportedPOVs.includes(pov));
    if (category !== "All") defs = defs.filter((d) => d.category === category);
    if (search) {
      const q = search.toLowerCase();
      defs = defs.filter((d) => d.name.toLowerCase().includes(q) || d.description.toLowerCase().includes(q));
    }
    defs = defs.filter((d) => {
      if (d.requiredPermission && !userPermissions.includes(d.requiredPermission)) return false;
      if (d.requiredPlugin) return false;
      return true;
    });
    return defs;
  }, [pov, category, search, userPermissions]);

  const grouped = React.useMemo(() => {
    const groups: Record<string, WidgetDefinition[]> = {};
    definitions.forEach((d) => {
      (groups[d.category] ??= []).push(d);
    });
    return groups;
  }, [definitions]);

  return (
    <SideSheet
      open={open}
      onOpenChange={(nextOpen) => { if (!nextOpen) onClose(); }}
      title="Widget Library"
      description={<>Browse and add widgets to your {pov} dashboard</>}
      size="md"
      titleClassName="text-sm"
      descriptionClassName="text-xs"
    >
      <SideSheetBody className="flex flex-col overflow-hidden">

        <div className="relative my-3">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Search widgets..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-7 h-8 text-xs"
          />
        </div>

        <div className="flex gap-1 flex-wrap mb-3">
          <Badge variant={category === "All" ? "default" : "outline"} className="cursor-pointer text-[10px]" onClick={() => setCategory("All")}>All</Badge>
          {categories.map((cat) => (
            <Badge key={cat} variant={category === cat ? "default" : "outline"} className="cursor-pointer text-[10px]" onClick={() => setCategory(cat)}>{cat}</Badge>
          ))}
        </div>

        <ScrollArea className="min-h-0 flex-1">
          <div className="space-y-4 pr-2">
            {Object.entries(grouped).map(([cat, widgets]) => (
              <div key={cat}>
                <h4 className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">{cat}</h4>
                <div className="space-y-1">
                  {widgets.map((w) => (
                    <div
                      key={w.type}
                      className="flex items-center gap-2 px-2.5 py-2 rounded-lg border border-border/40 hover:border-primary/30 hover:bg-accent/50 transition-colors"
                    >
                      <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                        <Eye className="h-3.5 w-3.5" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-xs font-medium truncate">{w.name}</div>
                        <div className="text-[9px] text-muted-foreground truncate">{w.description}</div>
                        {w.dataSourceLabel && (
                          <span className="text-[8px] text-muted-foreground/40">{w.dataSourceLabel}</span>
                        )}
                      </div>
                      <Button
                        variant="default"
                        size="icon"
                        className="h-7 w-7 shrink-0"
                        onClick={() => onAddWidget(w.type)}
                      >
                        <Plus className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  ))}
                </div>
              </div>
            ))}
            {definitions.length === 0 && (
              <p className="text-xs text-muted-foreground text-center py-8">No widgets found</p>
            )}
          </div>
        </ScrollArea>
      </SideSheetBody>
    </SideSheet>
  );
}
