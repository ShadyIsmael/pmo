import { ChevronRight, Files, Folder, FolderCog, FolderOpen, LibraryBig, Sparkles, Upload } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";

export interface LibraryCategory {
  id: string;
  name: string;
  /** Only supply a count when it covers the full authorized scope. */
  count?: number;
}

export function DocumentLibraryHeader({ canUpload, canManageCategories = false, onUpload }: { canUpload: boolean; canManageCategories?: boolean; onUpload: () => void }) {
  const { t } = useI18n();
  return (
    <header className="relative flex flex-col gap-2.5 rounded-xl border border-border/80 bg-card/90 px-4 py-3 shadow-2xs sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <div className="flex items-center gap-1.5 text-[11px] font-medium text-muted-foreground">
          <span className="inline-flex items-center gap-1 rounded-md border border-primary/25 bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold text-primary">
            <Sparkles className="h-2.5 w-2.5" aria-hidden="true" />
            <span>{t("documents.breadcrumbGovernance")}</span>
          </span>
          <span aria-hidden="true">/</span>
          <span className="font-medium text-foreground/80">{t("documents.breadcrumbCenter")}</span>
        </div>
        <h1 className="mt-0.5 text-lg font-bold tracking-tight text-foreground sm:text-xl">
          {t("documents.libraryTitle")}
        </h1>
      </div>
      {canUpload || canManageCategories ? (
        <div className="flex flex-wrap items-center gap-2">
          {canManageCategories ? (
            <Button asChild variant="outline" size="sm" className="h-8 text-xs hover:border-primary/40">
              <Link to="/administration/reference-data/document-categories">
                <FolderCog aria-hidden="true" className="me-1.5 h-3.5 w-3.5 text-muted-foreground" />
                {t("documents.manageCategories")}
              </Link>
            </Button>
          ) : null}
          {canUpload ? (
            <Button size="sm" onClick={onUpload} className="h-8 text-xs shadow-2xs">
              <Upload aria-hidden="true" className="me-1.5 h-3.5 w-3.5" />
              {t("documents.upload")}
            </Button>
          ) : null}
        </div>
      ) : null}
    </header>
  );
}

const folderColors = [
  "border-blue-500/25 bg-blue-500/10 text-blue-700 dark:text-blue-300",
  "border-violet-500/25 bg-violet-500/10 text-violet-700 dark:text-violet-300",
  "border-amber-500/25 bg-amber-500/10 text-amber-700 dark:text-amber-300",
  "border-emerald-500/25 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  "border-rose-500/25 bg-rose-500/10 text-rose-700 dark:text-rose-300",
  "border-cyan-500/25 bg-cyan-500/10 text-cyan-700 dark:text-cyan-300",
];

function categoryColor(id: string) {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (Math.imul(hash, 31) + id.charCodeAt(i)) | 0;
  return folderColors[(hash >>> 0) % folderColors.length];
}

export function DocumentLibraryOverview({ totalDocuments, activeTemplates, categories, selectedCategoryId, onSelectCategory, loadingCategories = false, folderSearch = "" }: {
  totalDocuments?: number;
  activeTemplates?: number;
  categories?: LibraryCategory[];
  selectedCategoryId: string;
  onSelectCategory: (id: string) => void;
  loadingCategories?: boolean;
  folderSearch?: string;
}) {
  const { t, formatNumber } = useI18n();
  const visibleCategories = categories?.filter((category) => category.name.toLocaleLowerCase().includes(folderSearch.trim().toLocaleLowerCase()));
  const metrics = [
    { label: t("documents.totalDocuments"), value: totalDocuments, icon: Files, color: "bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-500/20" },
    { label: t("documents.categoryCount"), value: categories?.length, icon: Folder, color: "bg-violet-500/10 text-violet-700 dark:text-violet-300 border-violet-500/20" },
    { label: t("documents.activeTemplates"), value: activeTemplates, icon: LibraryBig, color: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/20" },
  ];

  return (
    <div className="space-y-4">
      {/* Compact KPI Row */}
      <div className="grid gap-2.5 sm:grid-cols-3">
        {metrics.map(({ label, value, icon: Icon, color }) => (
          <div
            key={label}
            className="nexus-metric-card group flex min-h-[4.25rem] items-center gap-3 rounded-xl border border-border/80 bg-card/90 px-3.5 py-2.5 text-card-foreground shadow-2xs transition-all duration-150 hover:border-primary/40"
          >
            <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border shadow-2xs transition-transform duration-150 group-hover:scale-105 ${color}`}>
              <Icon aria-hidden="true" className="h-4.5 w-4.5" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-xl font-bold tracking-tight tabular-nums text-foreground sm:text-2xl leading-none">
                {value === undefined ? (
                  <>
                    <span aria-hidden="true">—</span>
                    <span className="sr-only">{t("documents.unavailable")}</span>
                  </>
                ) : (
                  formatNumber(value)
                )}
              </p>
              <p className="mt-1 text-[11px] font-medium text-muted-foreground truncate">{label}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Compact Category Shelf */}
      <section aria-labelledby="library-browse-title" className="space-y-2">
        <h2 id="library-browse-title" className="text-sm font-bold tracking-tight text-foreground">
          {t("documents.browseLibrary")}
        </h2>

        {loadingCategories ? (
          <p className="text-xs text-muted-foreground" role="status">
            {t("common.loading")}
          </p>
        ) : categories && categories.length === 0 ? (
          <p className="rounded-lg border border-dashed border-border/80 bg-card/40 p-4 text-center text-xs text-muted-foreground">
            {t("documents.foldersEmpty")}
          </p>
        ) : null}

        {visibleCategories?.length ? (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
            {visibleCategories.map((category) => {
              const selected = selectedCategoryId === category.id;
              return (
                <button
                  key={category.id}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => onSelectCategory(category.id)}
                  className={`group relative flex min-w-0 items-center justify-between gap-2.5 rounded-lg border bg-card px-2.5 py-2 text-start transition-all duration-150 hover:border-primary/40 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring ${
                    selected ? "border-primary bg-primary/[0.05] ring-1 ring-primary/40 shadow-2xs" : "border-border/80"
                  }`}
                >
                  <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-md border transition-transform duration-150 group-hover:scale-105 ${categoryColor(category.id)}`}>
                    <FolderOpen aria-hidden="true" className="h-4 w-4" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-xs font-semibold text-foreground transition-colors group-hover:text-primary">
                      {category.name}
                    </span>
                    <span className="block truncate text-[10px] font-medium text-muted-foreground">
                      {category.count === undefined ? t("documents.unavailable") : t("documents.folderDocumentCount", { count: formatNumber(category.count) })}
                    </span>
                  </span>
                  <ChevronRight aria-hidden="true" className={`h-3 w-3 shrink-0 transition-opacity ${selected ? "text-primary opacity-100" : "text-muted-foreground/30 opacity-0 group-hover:opacity-100"}`} />
                </button>
              );
            })}
          </div>
        ) : null}
      </section>
    </div>
  );
}
