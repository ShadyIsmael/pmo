"use client";

import * as React from "react";
import { X } from "lucide-react";
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { cn } from "@/lib/utils";

export type SideSheetSize = "sm" | "md" | "lg" | "xl" | "full";

const sideSheetSizes: Record<SideSheetSize, string> = {
  sm: "sm:max-w-xs",
  md: "sm:max-w-lg",
  lg: "sm:max-w-2xl lg:max-w-3xl",
  xl: "sm:max-w-3xl lg:max-w-5xl",
  full: "sm:max-w-[calc(100vw-2rem)]",
};

export interface SideSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  /** Content displayed before the title, such as an icon or badge. */
  headerLeading?: React.ReactNode;
  /** Uses logical page direction: end is right in LTR and left in RTL. */
  side?: "start" | "end";
  size?: SideSheetSize;
  closeLabel?: string;
  className?: string;
  headerClassName?: string;
  titleClassName?: string;
  descriptionClassName?: string;
}

export function SideSheet({
  open,
  onOpenChange,
  title,
  description,
  children,
  headerLeading,
  side = "end",
  size = "lg",
  closeLabel = "Close",
  className,
  headerClassName,
  titleClassName,
  descriptionClassName,
}: SideSheetProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side={side}
        showClose={false}
        className={cn(
          "flex w-full flex-col gap-0 overflow-hidden p-0",
          sideSheetSizes[size],
          className
        )}
      >
        <SheetHeader
          className={cn(
            "shrink-0 space-y-0 border-b px-4 py-5 text-start sm:px-6",
            headerClassName
          )}
        >
          <div className="flex items-start gap-3">
            {headerLeading ? <div className="shrink-0">{headerLeading}</div> : null}
            <div className="min-w-0 flex-1">
              <SheetTitle className={cn("text-base", titleClassName)}>{title}</SheetTitle>
              {description ? (
                <SheetDescription className={cn("mt-1", descriptionClassName)}>
                  {description}
                </SheetDescription>
              ) : null}
            </div>
            <SheetClose className="shrink-0 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:pointer-events-none">
              <X className="h-4 w-4" />
              <span className="sr-only">{closeLabel}</span>
            </SheetClose>
          </div>
        </SheetHeader>
        {children}
      </SheetContent>
    </Sheet>
  );
}

export function SideSheetBody({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        "min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-4 py-5 sm:px-6",
        className
      )}
      {...props}
    />
  );
}

export function SideSheetFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        "flex shrink-0 flex-col-reverse gap-2 border-t bg-background px-4 py-4 sm:flex-row sm:justify-end sm:px-6",
        className
      )}
      {...props}
    />
  );
}
