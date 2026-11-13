import { Building2, Download, FileSpreadsheet, FileText, File } from "lucide-react";
import { Button } from "@components/ui/Button";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { formatFileSize } from "@lib/formatting/file-size";
import { formatDate } from "@lib/utils";
import type { DocumentListItem } from "./api/documents";

function fileAppearance(item: DocumentListItem) {
  const type = (item.fileType || item.fileName || "").toLowerCase();
  if (/pdf/.test(type)) return { Icon: FileText, color: "bg-red-500/10 text-red-700 dark:text-red-300", badgeColor: "bg-red-500/10 text-red-700 dark:text-red-300 border-red-500/20", ext: "PDF" };
  if (/xls|excel|spreadsheet/.test(type)) return { Icon: FileSpreadsheet, color: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300", badgeColor: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/20", ext: "XLSX" };
  if (/doc|word/.test(type)) return { Icon: FileText, color: "bg-blue-500/10 text-blue-700 dark:text-blue-300", badgeColor: "bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-500/20", ext: "DOCX" };
  return { Icon: File, color: "bg-muted text-muted-foreground", badgeColor: "bg-muted text-muted-foreground border-border", ext: item.fileType || "DOC" };
}

export function LibraryDocuments({ items, view, loading, error, filtered, onRetry, onOpen, onDownload }: {
  items: readonly DocumentListItem[];
  view: "grid" | "list";
  loading: boolean;
  error: boolean;
  filtered: boolean;
  onRetry: () => void;
  onOpen: (id: string) => void;
  onDownload: (item: DocumentListItem) => void;
}) {
  const { t, formatNumber, locale } = useI18n();

  if (loading) {
    return (
      <div className={view === "grid" ? "grid gap-2.5 sm:grid-cols-2 xl:grid-cols-3" : "space-y-2"}>
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <Skeleton key={i} className={view === "grid" ? "h-36 rounded-xl" : "h-14 rounded-lg"} />
        ))}
      </div>
    );
  }

  if (error) return <ErrorState title={t("documents.loadError")} onRetry={onRetry} />;

  if (!items.length) {
    return (
      <EmptyState
        icon={<FileText className="h-9 w-9 text-muted-foreground/60" />}
        title={t(filtered ? "documents.libraryNoResults" : "documents.emptyTitle")}
        description={t(filtered ? "documents.libraryNoResultsDescription" : "documents.emptyDescription")}
      />
    );
  }

  if (view === "grid") {
    return (
      <div className="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-3">
        {items.map((item) => {
          const { Icon, color, badgeColor, ext } = fileAppearance(item);
          return (
            <article
              key={item.id}
              className="group relative flex flex-col justify-between overflow-hidden rounded-xl border border-border/80 bg-card p-3 shadow-2xs transition-all duration-150 hover:border-primary/40 hover:shadow-xs"
            >
              <div>
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <span className={`inline-flex items-center rounded px-1.5 py-0.2 text-[9px] font-bold tracking-wider uppercase border ${badgeColor}`}>
                      {ext}
                    </span>
                    <span className="text-[11px] font-semibold text-muted-foreground tabular-nums">
                      {t("documents.table.version")}: v{formatNumber(item.version)}
                    </span>
                  </div>
                  <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.2 text-[10px] font-semibold ${item.isArchived ? "bg-muted text-muted-foreground" : "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300"}`}>
                    <span className={`h-1.5 w-1.5 rounded-full ${item.isArchived ? "bg-muted-foreground" : "bg-emerald-500"}`} />
                    {item.isArchived ? t("documents.status.archived") : t("documents.status.active")}
                  </span>
                </div>

                <div className="my-2.5 flex items-start gap-2.5">
                  <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border shadow-2xs transition-transform duration-150 group-hover:scale-105 ${color}`}>
                    <Icon aria-hidden="true" className="h-5 w-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <button
                      type="button"
                      onClick={() => onOpen(item.id)}
                      className="block max-w-full truncate text-start text-xs font-bold text-foreground transition-colors hover:text-primary focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    >
                      {item.name}
                    </button>
                    <p className="mt-0.5 truncate text-[11px] font-medium text-muted-foreground">
                      {item.categoryName || t("documents.categoryUnavailable")}
                    </p>
                    {item.organizationName ? (
                      <p className="mt-0.5 flex items-center gap-1 truncate text-[10px] text-muted-foreground/85">
                        <Building2 className="h-3 w-3 shrink-0" aria-hidden="true" />
                        <span className="truncate">{item.organizationName}</span>
                      </p>
                    ) : null}
                  </div>
                </div>
              </div>

              <div className="mt-0.5 flex items-center justify-between border-t border-border/50 pt-2 text-[11px] text-muted-foreground">
                <div className="flex items-center gap-1.5 tabular-nums">
                  {item.fileSize ? <span>{formatFileSize(item.fileSize, locale)}</span> : null}
                  {item.fileSize && (item.updatedAt || item.createdAt) ? <span aria-hidden="true">•</span> : null}
                  {item.updatedAt || item.createdAt ? <span>{formatDate(item.updatedAt || item.createdAt, locale)}</span> : null}
                  <span className="sr-only">{t("documents.table.type")}: {item.fileType || t("documents.unavailable")}</span>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 shrink-0 rounded-md hover:bg-primary/10 hover:text-primary"
                  aria-label={t("documents.download")}
                  onClick={() => onDownload(item)}
                >
                  <Download className="h-3.5 w-3.5" />
                </Button>
              </div>
            </article>
          );
        })}
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {items.map((item) => {
        const { Icon, color } = fileAppearance(item);
        return (
          <article
            key={item.id}
            className="group flex flex-col gap-2 rounded-lg border border-border/80 bg-card px-3 py-2 shadow-2xs transition-all duration-150 hover:border-primary/40 sm:flex-row sm:items-center sm:justify-between"
          >
            <div className="flex min-w-0 flex-1 items-center gap-2.5">
              <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border shadow-2xs transition-transform duration-150 group-hover:scale-105 ${color}`}>
                <Icon aria-hidden="true" className="h-4.5 w-4.5" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onOpen(item.id)}
                    className="truncate text-start text-xs font-bold text-foreground transition-colors hover:text-primary focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                  >
                    {item.name}
                  </button>
                </div>
                <div className="flex flex-wrap items-center gap-x-2 text-[11px] text-muted-foreground">
                  <span className="font-medium text-foreground/80">{item.categoryName || t("documents.categoryUnavailable")}</span>
                  {item.organizationName ? (
                    <>
                      <span aria-hidden="true">•</span>
                      <span className="truncate">{item.organizationName}</span>
                    </>
                  ) : null}
                </div>
              </div>
            </div>

            <div className="flex items-center justify-between gap-3 border-t border-border/40 pt-1.5 text-[11px] text-muted-foreground sm:border-t-0 sm:pt-0">
              <div className="flex flex-wrap items-center gap-x-2.5 gap-y-0.5">
                <span className="tabular-nums font-semibold">{t("documents.table.version")}: v{formatNumber(item.version)}</span>
                <span className="hidden sm:inline" aria-hidden="true">•</span>
                <span>{t("documents.table.type")}: {item.fileType || t("documents.unavailable")}</span>
                <span className="hidden sm:inline" aria-hidden="true">•</span>
                <span className={`font-semibold ${item.isArchived ? "text-muted-foreground" : "text-emerald-600 dark:text-emerald-400"}`}>
                  {item.isArchived ? t("documents.status.archived") : t("documents.status.active")}
                </span>
                {item.fileSize ? (
                  <>
                    <span className="hidden md:inline" aria-hidden="true">•</span>
                    <span className="hidden md:inline tabular-nums">{formatFileSize(item.fileSize, locale)}</span>
                  </>
                ) : null}
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7 shrink-0 rounded-md hover:bg-primary/10 hover:text-primary"
                aria-label={t("documents.download")}
                onClick={() => onDownload(item)}
              >
                <Download className="h-3.5 w-3.5" />
              </Button>
            </div>
          </article>
        );
      })}
    </div>
  );
}
