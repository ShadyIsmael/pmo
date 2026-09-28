import * as React from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface TablePaginationProps {
  page: number;
  totalPages: number;
  summary: React.ReactNode;
  previousLabel: string;
  nextLabel: string;
  onPageChange: (_page: number) => void;
  disabled?: boolean;
  footerNote?: React.ReactNode;
  className?: string;
}

function TablePagination({
  page,
  totalPages,
  summary,
  previousLabel,
  nextLabel,
  onPageChange,
  disabled = false,
  footerNote,
  className,
}: TablePaginationProps) {
  return (
    <nav aria-label={`${previousLabel} / ${nextLabel}`} className={cn("flex flex-wrap items-center justify-between gap-3 border-t border-border px-4 py-3 text-xs text-muted-foreground", className)}>
      <span className="tabular-nums">{summary}</span>
      {totalPages > 1 ? <div className="flex items-center gap-1">
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="h-8 gap-1 px-2.5"
          aria-label={previousLabel}
          disabled={disabled || page <= 1}
          onClick={() => onPageChange(Math.max(1, page - 1))}
        >
          <ChevronLeft className="h-3.5 w-3.5 rtl:rotate-180" />
          <span className="hidden sm:inline">{previousLabel}</span>
        </Button>
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="h-8 gap-1 px-2.5"
          aria-label={nextLabel}
          disabled={disabled || page >= totalPages}
          onClick={() => onPageChange(Math.min(totalPages, page + 1))}
        >
          <span className="hidden sm:inline">{nextLabel}</span>
          <ChevronRight className="h-3.5 w-3.5 rtl:rotate-180" />
        </Button>
      </div> : footerNote ? <span>{footerNote}</span> : null}
    </nav>
  );
}

export { TablePagination };
