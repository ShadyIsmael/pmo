"use client";

import * as React from "react";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowUpRight, CalendarDays, FolderKanban, Layers3, Loader2, Search, UsersRound } from "lucide-react";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { cn } from "@/lib/utils";
import { searchNexus, type SearchEntityType, type SearchResult } from "@/lib/global-search";
import type { RecentEntity } from "@/lib/recent-opened";
import { rememberRecent } from "@/lib/recent-opened";
import { useAuth } from "@/lib/auth";

interface GlobalSearchPaletteProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  recent: RecentEntity[];
  onRecentChange: (items: RecentEntity[]) => void;
}

const icons: Record<SearchEntityType, React.ComponentType<{ className?: string }>> = {
  project: FolderKanban,
  program: Layers3,
  portfolio: Layers3,
  resource: UsersRound,
  meeting: CalendarDays,
};

export function GlobalSearchPalette({ open, onOpenChange, recent, onRecentChange }: GlobalSearchPaletteProps) {
  const router = useRouter();
  const pathname = usePathname();
  const { user } = useAuth();
  const t = useTranslations("nav");
  const [query, setQuery] = React.useState("");
  const [results, setResults] = React.useState<SearchResult[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [activeIndex, setActiveIndex] = React.useState(0);
  const inputRef = React.useRef<HTMLInputElement>(null);

  React.useEffect(() => {
    if (!open) return;
    setQuery(""); setResults([]); setActiveIndex(0);
    const timer = window.setTimeout(() => inputRef.current?.focus(), 80);
    return () => window.clearTimeout(timer);
  }, [open]);

  React.useEffect(() => {
    const value = query.trim();
    if (value.length < 2) { setResults([]); setLoading(false); return; }
    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setLoading(true);
      try { const found = await searchNexus(value); if (!cancelled) { setResults(found); setActiveIndex(0); } }
      finally { if (!cancelled) setLoading(false); }
    }, 280);
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [query]);

  const locale = pathname.split("/").filter(Boolean)[0] || "en";
  const options = query.trim().length >= 2 ? results : recent;

  function openResult(result: SearchResult | RecentEntity) {
    const next = rememberRecent({ id: result.id, type: result.type, name: result.name, href: result.href, code: result.code, context: result.context }, user?.tenantId);
    onRecentChange(next);
    onOpenChange(false);
    router.push(`/${locale}${result.href}`);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown") { event.preventDefault(); setActiveIndex((value) => Math.min(value + 1, Math.max(options.length - 1, 0))); }
    if (event.key === "ArrowUp") { event.preventDefault(); setActiveIndex((value) => Math.max(value - 1, 0)); }
    if (event.key === "Enter" && options[activeIndex]) { event.preventDefault(); openResult(options[activeIndex]); }
  }

  const groups = query.trim().length >= 2 ? Array.from(new Set(results.map((item) => item.type))) : [];
  const typeLabel = (type: SearchEntityType) => ({ project: t("searchProjects"), program: t("searchPrograms"), portfolio: t("searchPortfolios"), resource: t("searchPeople"), meeting: t("searchMeetings") }[type]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[calc(100vw-1rem)] max-w-[640px] overflow-hidden rounded-lg border border-border bg-background/95 p-0 shadow-2xl backdrop-blur-sm sm:w-full">
        <DialogTitle className="sr-only">{t("searchNexus")}</DialogTitle>
        <DialogDescription className="sr-only">{t("searchHint")}</DialogDescription>
        <div className="flex h-12 items-center gap-3 border-b border-border px-4">
          <Search className="h-4 w-4 shrink-0 text-primary" />
          <input ref={inputRef} value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={handleKeyDown} placeholder={t("searchPlaceholder")} aria-label={t("searchNexus")} className="h-full min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground/70" />
          {loading ? <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /> : <kbd className="hidden rounded border border-border bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground sm:inline">Esc</kbd>}
        </div>
        <DialogBody className="max-h-[70vh] space-y-4 p-2 sm:p-3">
          {query.trim().length < 2 ? (
            recent.length > 0 ? <ResultGroup title={t("recentOpened")} items={recent} activeIndex={activeIndex} onOpen={openResult} onHover={setActiveIndex} /> : <div className="px-4 py-10 text-center text-sm text-muted-foreground">{t("searchHint")}</div>
          ) : loading && results.length === 0 ? (
            <div className="space-y-2 p-2">{[1, 2, 3].map((item) => <div key={item} className="h-12 animate-pulse rounded-md bg-muted" />)}</div>
          ) : results.length === 0 ? (
            <div className="px-4 py-10 text-center text-sm text-muted-foreground">{t("searchNoResults", { query: query.trim() })}</div>
          ) : groups.map((type) => <ResultGroup key={type} title={typeLabel(type)} items={results.filter((item) => item.type === type)} activeIndex={activeIndex} globalOffset={results.findIndex((item) => item.type === type)} onOpen={openResult} onHover={setActiveIndex} />)}
          <div className="flex items-center justify-end gap-3 border-t border-border px-2 pt-2 text-[10px] text-muted-foreground">
            <span>↑ ↓ {t("searchNavigate")}</span>
            <span>Enter {t("searchOpen")}</span>
            <span>Esc {t("searchClose")}</span>
          </div>
        </DialogBody>
      </DialogContent>
    </Dialog>
  );
}

function ResultGroup({ title, items, activeIndex, globalOffset = 0, onOpen, onHover }: { title: string; items: readonly (SearchResult | RecentEntity)[]; activeIndex: number; globalOffset?: number; onOpen: (item: SearchResult | RecentEntity) => void; onHover: (index: number) => void }) {
  return (
    <section>
      <h3 className="px-2 pb-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{title}</h3>
      <div className="space-y-0.5">
        {items.map((item, index) => {
          const Icon = icons[item.type];
          const position = globalOffset + index;
          return (
            <button key={`${item.type}-${item.id}`} type="button" onMouseEnter={() => onHover(position)} onClick={() => onOpen(item)} className={cn(
              "group relative flex min-h-[48px] w-full items-center gap-3 rounded-md px-3 py-2 text-start transition-colors duration-150",
              activeIndex === position ? "bg-primary/10 text-foreground" : "hover:bg-muted/60"
            )}>
              <span className={cn(
                "flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground",
                activeIndex === position && "bg-primary/15 text-primary"
              )}>
                <Icon className="h-4 w-4" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">{item.name}</span>
                <span className="block truncate text-xs text-muted-foreground">
                  {item.type} {item.code ? `· ${item.code}` : ""}{item.context ? ` · ${item.context}` : ""}{"status" in item && item.status ? ` · ${item.status}` : ""}
                </span>
              </span>
              <ArrowUpRight className="h-4 w-4 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100" />
            </button>
          );
        })}
      </div>
    </section>
  );
}
