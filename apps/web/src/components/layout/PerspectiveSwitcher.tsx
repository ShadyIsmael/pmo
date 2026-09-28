"use client";

import { Check, ChevronDown, Eye } from "lucide-react";
import { useTranslations } from "next-intl";
import { useManagementPerspective } from "@/lib/perspective-context";
import { MANAGEMENT_PERSPECTIVES } from "@/lib/perspective";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

export function PerspectiveSwitcher() {
  const t = useTranslations("perspective");
  const { current, availablePerspectives, effectiveReadOnly, switchPerspective, exitViewAs } = useManagementPerspective();
  if (!current || availablePerspectives.length === 0) return null;

  const label = t(current.perspective);
  const selectedId = current.perspective === MANAGEMENT_PERSPECTIVES.PortfolioManager ? current.portfolioId : current.perspective === MANAGEMENT_PERSPECTIVES.ProgramManager ? current.programId : current.projectId;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" className="hidden h-8 gap-1.5 px-2.5 text-xs font-medium md:inline-flex">
          <Eye className="h-3.5 w-3.5" />
          <span>{t("viewingAs")}: {label}</span>
          {effectiveReadOnly && <Badge status="ReadOnly" className="h-4 px-1 text-[9px]">{t("readOnly")}</Badge>}
          <ChevronDown className="h-3 w-3 text-muted-foreground/60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64 p-1">
        <DropdownMenuLabel>{t("nativeView")}</DropdownMenuLabel>
        {availablePerspectives.filter((item) => item.isNative).map((item) => (
          item.contexts.length > 1 ? (
            <div key={item.perspective} className="space-y-1 px-2 py-1.5">
              <p className="text-xs font-medium text-muted-foreground">{t(item.perspective)}</p>
              <Select value={current.perspective === item.perspective ? selectedId : undefined} onValueChange={(id) => switchPerspective(item.perspective, id)}>
                <SelectTrigger className="h-8 text-xs"><SelectValue placeholder={t(item.perspective)} /></SelectTrigger>
                <SelectContent>
                  {item.contexts.map((context) => <SelectItem key={context.id} value={context.id}>{context.name}{context.code ? ` · ${context.code}` : ""}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          ) : (
            <DropdownMenuItem key={item.perspective} className="rounded-md px-2 py-1.5 text-sm" onSelect={() => switchPerspective(item.perspective)}>
              <span className="flex-1">{t(item.perspective)}</span>
              {current.perspective === item.perspective && !effectiveReadOnly && <Check className="h-4 w-4 text-primary" />}
            </DropdownMenuItem>
          )
        ))}
        {availablePerspectives.some((item) => !item.isNative) && <DropdownMenuSeparator />}
        {availablePerspectives.filter((item) => !item.isNative).map((item) => (
          <div key={item.perspective} className="space-y-1 px-2 py-1.5">
            <p className="text-xs font-medium text-muted-foreground">{t("viewAs", { perspective: t(item.perspective) })}</p>
            <Select value={current.perspective === item.perspective ? selectedId : undefined} onValueChange={(id) => switchPerspective(item.perspective, id)}>
              <SelectTrigger className="h-8 text-xs"><SelectValue placeholder={t(item.perspective === MANAGEMENT_PERSPECTIVES.ProgramManager ? "selectProgram" : "selectProject")} /></SelectTrigger>
              <SelectContent>
                {item.contexts.map((context) => <SelectItem key={context.id} value={context.id}>{context.name}{context.code ? ` · ${context.code}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        ))}
        {effectiveReadOnly && <>
          <DropdownMenuSeparator />
          <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm" onSelect={exitViewAs}>{t("exitView")}</DropdownMenuItem>
          <p className="px-2 pb-1 pt-0.5 text-[10px] text-muted-foreground">{t("readOnlyDescription")}</p>
        </>}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
