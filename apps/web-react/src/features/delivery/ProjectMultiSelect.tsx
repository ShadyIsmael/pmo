import * as React from "react";
import { createPortal } from "react-dom";
import { Check, ChevronDown, Loader2, X } from "lucide-react";
import { cn } from "@lib/utils";
import { useI18n } from "@i18n/messages";
import { useAnchoredOverlayPosition } from "@components/ui/AnchoredOverlay";
import { projectColor } from "./meetings-calendar";

export interface ProjectFilterOption {
  publicId: string;
  name: string;
  code?: string | null;
}

interface ProjectMultiSelectProps {
  options: readonly ProjectFilterOption[];
  selected: readonly string[];
  onChange: (selected: string[]) => void;
  loading?: boolean;
  disabled?: boolean;
  className?: string;
}

const MAX_RENDERED_OPTIONS = 200;
const MAX_VISIBLE_CHIPS = 8;

export function ProjectMultiSelect({ options, selected, onChange, loading = false, disabled = false, className }: ProjectMultiSelectProps) {
  const { t } = useI18n();
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const contentRef = React.useRef<HTMLDivElement>(null);
  const overlayStyle = useAnchoredOverlayPosition({ open, anchorRef: triggerRef, contentRef, matchTriggerWidth: true });

  React.useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!triggerRef.current?.contains(target) && !contentRef.current?.contains(target)) {
        setOpen(false);
        setQuery("");
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        setQuery("");
      }
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  const selectedSet = React.useMemo(() => new Set(selected), [selected]);
  const allSelected = options.length > 0 && selected.length === options.length;
  const term = query.trim().toLowerCase();
  const filtered = term
    ? options.filter((option) => option.name.toLowerCase().includes(term) || (option.code ?? "").toLowerCase().includes(term))
    : options;
  const rendered = filtered.slice(0, MAX_RENDERED_OPTIONS);
  const selectedOptions = options.filter((option) => selectedSet.has(option.publicId));
  const visibleSelectedOptions = selectedOptions.slice(0, MAX_VISIBLE_CHIPS);
  const hiddenSelectedCount = selectedOptions.length - visibleSelectedOptions.length;

  const triggerLabel = selected.length === 0
    ? t("meetings.projectFilter.noneSelected")
    : allSelected
      ? t("meetings.projectFilter.allProjects")
      : t("meetings.projectFilter.selected", { count: selected.length });

  const toggle = (publicId: string) => {
    onChange(selectedSet.has(publicId) ? selected.filter((id) => id !== publicId) : [...selected, publicId]);
  };

  return (
    <div className={cn("w-full space-y-2", className)}>
      <button
        ref={triggerRef}
        type="button"
        role="combobox"
        aria-label={t("meetings.projectFilter.label")}
        aria-expanded={open}
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={() => setOpen((value) => !value)}
        className="flex h-10 w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm font-normal ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <span className={cn("min-w-0 truncate", selected.length === 0 && "text-muted-foreground")}>{triggerLabel}</span>
        <ChevronDown className="h-4 w-4 shrink-0 opacity-50" aria-hidden="true" />
      </button>

      {selectedOptions.length > 0 ? (
        <div className="flex max-h-24 flex-wrap gap-1.5 overflow-y-auto" aria-label={t("meetings.projectFilter.selectedProjects")}>
          {visibleSelectedOptions.map((option) => {
            const color = projectColor(option.publicId);
            return (
              <span key={option.publicId} className="inline-flex max-w-full items-center gap-1.5 rounded-full border bg-background py-0.5 pe-1 ps-2 text-xs">
                <span className={cn("h-2 w-2 shrink-0 rounded-full", color.dot)} aria-hidden="true" />
                <span className="truncate">{option.name}</span>
                <button
                  type="button"
                  onClick={() => toggle(option.publicId)}
                  aria-label={t("meetings.projectFilter.remove", { name: option.name })}
                  className="rounded-full p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <X className="h-3 w-3" aria-hidden="true" />
                </button>
              </span>
            );
          })}
          {hiddenSelectedCount > 0 ? (
            <span className="inline-flex items-center rounded-full border border-dashed px-2 py-0.5 text-xs text-muted-foreground">
              {t("meetings.projectFilter.moreSelected", { count: hiddenSelectedCount })}
            </span>
          ) : null}
        </div>
      ) : null}

      {open ? createPortal(
        <div ref={contentRef} role="listbox" aria-multiselectable="true" style={overlayStyle} className="max-h-80 overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md">
          <div className="space-y-2 border-b p-2">
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t("meetings.searchProjects")}
              aria-label={t("meetings.searchProjects")}
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
            <div className="flex items-center justify-between gap-2">
              <button type="button" onClick={() => onChange(options.map((option) => option.publicId))} disabled={options.length === 0} className="text-xs font-medium text-primary hover:underline disabled:opacity-50">
                {t("meetings.projectFilter.selectAll")}
              </button>
              <button type="button" onClick={() => onChange([])} disabled={selected.length === 0} className="text-xs font-medium text-muted-foreground hover:text-foreground disabled:opacity-50">
                {t("meetings.projectFilter.clearAll")}
              </button>
            </div>
          </div>
          <div className="max-h-56 overflow-auto p-1">
            {loading ? (
              <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                {t("common.loading")}
              </div>
            ) : filtered.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted-foreground">{t("meetings.noProjects")}</p>
            ) : (
              <>
                {rendered.map((option) => {
                  const checked = selectedSet.has(option.publicId);
                  const color = projectColor(option.publicId);
                  return (
                    <button
                      key={option.publicId}
                      type="button"
                      role="option"
                      aria-selected={checked}
                      onClick={() => toggle(option.publicId)}
                      className={cn(
                        "flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-start text-sm hover:bg-accent hover:text-accent-foreground",
                        checked && "bg-accent/60 font-medium",
                      )}
                    >
                      <span className={cn("flex h-4 w-4 shrink-0 items-center justify-center rounded border", checked ? "border-primary bg-primary text-primary-foreground" : "border-input")} aria-hidden="true">
                        {checked ? <Check className="h-3 w-3" /> : null}
                      </span>
                      <span className={cn("h-2.5 w-2.5 shrink-0 rounded-full", color.dot)} aria-hidden="true" />
                      <span className="min-w-0 flex-1 truncate">{option.name}</span>
                      {option.code ? <span className="shrink-0 text-[11px] text-muted-foreground">{option.code}</span> : null}
                    </button>
                  );
                })}
                {filtered.length > rendered.length ? (
                  <p className="px-2 py-2 text-center text-xs text-muted-foreground">{t("meetings.projectFilter.refineSearch", { count: filtered.length })}</p>
                ) : null}
              </>
            )}
          </div>
        </div>,
        document.body,
      ) : null}
    </div>
  );
}
