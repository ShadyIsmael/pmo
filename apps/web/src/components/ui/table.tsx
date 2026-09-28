import * as React from "react";
import { cn } from "@/lib/utils";

type TableDensity = "standard" | "compact";

interface TableProps extends React.HTMLAttributes<HTMLTableElement> {
  density?: TableDensity;
  containerClassName?: string;
}

const Table = React.forwardRef<HTMLTableElement, TableProps>(
  ({ className, containerClassName, density = "standard", ...props }, ref) => (
    <div className={cn("relative w-full overflow-auto rounded-lg border border-border bg-background shadow-none", containerClassName)}>
      <table ref={ref} className={cn("w-full caption-bottom text-sm", density === "compact" && "text-xs", className)} {...props} />
    </div>
  )
);
Table.displayName = "Table";

const TableHeader = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <thead ref={ref} className={cn("bg-muted/40 [&_tr]:border-b [&_tr]:border-border/70", className)} {...props} />
));
TableHeader.displayName = "TableHeader";

const TableBody = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tbody ref={ref} className={cn("[&_tr:last-child]:border-0", className)} {...props} />
));
TableBody.displayName = "TableBody";

const TableFooter = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tfoot ref={ref} className={cn("border-t border-border/70 bg-muted/30 font-medium [&>tr]:last:border-b-0", className)} {...props} />
));
TableFooter.displayName = "TableFooter";

const TableRow = React.forwardRef<HTMLTableRowElement, React.HTMLAttributes<HTMLTableRowElement>>(
  ({ className, ...props }, ref) => (
    <tr
      ref={ref}
      className={cn(
        "border-b border-border/50 transition-colors duration-150 hover:bg-muted/25 focus-within:bg-primary/[0.04] data-[state=selected]:bg-primary/[0.06] data-[state=selected]:hover:bg-primary/[0.09] data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-60",
        className
      )}
      {...props}
    />
  )
);
TableRow.displayName = "TableRow";

const TableHead = React.forwardRef<
  HTMLTableCellElement,
  React.ThHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <TableHeadCell ref={ref} className={className} {...props} />
));
TableHead.displayName = "TableHead";

const TableHeadCell = React.forwardRef<HTMLTableCellElement, React.ThHTMLAttributes<HTMLTableCellElement>>(
  ({ className, ...props }, ref) => {
    return (
      <th
        ref={ref}
        className={cn(
          "align-middle text-start text-[11px] font-semibold uppercase tracking-wider text-muted-foreground [&:has([role=checkbox])]:pe-0",
          "h-10 px-3 py-2.5",
          className
        )}
        {...props}
      />
    );
  }
);
TableHeadCell.displayName = "TableHeadCell";

const TableCell = React.forwardRef<
  HTMLTableCellElement,
  React.TdHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <TableCellContent ref={ref} className={className} {...props} />
));
TableCell.displayName = "TableCell";

const TableCellContent = React.forwardRef<HTMLTableCellElement, React.TdHTMLAttributes<HTMLTableCellElement>>(
  ({ className, ...props }, ref) => {
    return (
      <td
        ref={ref}
        className={cn(
          "align-middle text-sm [&:has([role=checkbox])]:pe-0",
          "px-3 py-2.5",
          className
        )}
        {...props}
      />
    );
  }
);
TableCellContent.displayName = "TableCellContent";

const TableCaption = React.forwardRef<
  HTMLTableCaptionElement,
  React.HTMLAttributes<HTMLTableCaptionElement>
>(({ className, ...props }, ref) => (
  <caption ref={ref} className={cn("mt-3 text-xs text-muted-foreground/70", className)} {...props} />
));
TableCaption.displayName = "TableCaption";

export { Table, TableHeader, TableBody, TableFooter, TableHead, TableRow, TableCell, TableCaption };
