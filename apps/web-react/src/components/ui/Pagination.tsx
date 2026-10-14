import * as React from "react";
import { Button } from "./Button";
import { useI18n } from "@i18n/messages";
import { cn } from "@lib/utils";

interface PaginationProps {
  page: number;
  totalPages: number;
  total?: number;
  pageSize?: number;
  onChange: (page: number) => void;
  className?: string;
}

export function Pagination({ page, totalPages, total, pageSize, onChange, className }: PaginationProps) {
  const { t } = useI18n();
  const safeTotal = Math.max(1, totalPages);
  const from = total !== undefined && pageSize !== undefined ? (page - 1) * pageSize + 1 : undefined;
  const to = total !== undefined && pageSize !== undefined ? Math.min(page * pageSize, total) : undefined;
  return (
    <div className={cn("flex flex-wrap items-center justify-between gap-3", className)}>
      <p className="text-sm text-muted-foreground">
        {from !== undefined && to !== undefined && total !== undefined
          ? t("common.paginationSummary", { from, to, total })
          : t("common.pageOf", { page, totalPages: safeTotal })}
      </p>
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onChange(page - 1)}>{t("common.previous")}</Button>
        <span className="text-sm tabular-nums text-muted-foreground">{page} / {safeTotal}</span>
        <Button variant="outline" size="sm" disabled={page >= safeTotal} onClick={() => onChange(page + 1)}>{t("common.next")}</Button>
      </div>
    </div>
  );
}
