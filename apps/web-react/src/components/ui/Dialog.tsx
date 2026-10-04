import * as React from "react";
import { cn } from "@lib/utils";

interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  children: React.ReactNode;
  className?: string;
}

const Dialog = React.forwardRef<HTMLDivElement, DialogProps>(
  ({ className, children, open, onOpenChange, ...props }, ref) => (
    <div ref={ref} className={cn("fixed inset-0 z-50", className)} {...props}>
      {open && <div className="fixed inset-0 bg-black/50 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" onClick={() => onOpenChange?.(false)} />}
      {open && (
        <div className="fixed left-1/2 top-1/2 z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border bg-background p-6 shadow-lg duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 sm:max-w-lg">
          {children}
        </div>
      )}
    </div>
  )
);
Dialog.displayName = "Dialog";

type DialogContentProps = React.HTMLAttributes<HTMLDivElement>;

const DialogContent = React.forwardRef<HTMLDivElement, DialogContentProps>(
  ({ className, children, ...props }, ref) => (
    <div ref={ref} className={cn("", className)} {...props}>
      {children}
    </div>
  )
);
DialogContent.displayName = "DialogContent";

const DialogHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...props }, ref) => (
    <div ref={ref} className={cn("flex flex-col space-y-1.5 text-center sm:text-left", className)} {...props}>
      {children}
    </div>
  )
);
DialogHeader.displayName = "DialogHeader";

const DialogTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, children, ...props }, ref) => (
    <h2 ref={ref} className={cn("text-lg font-semibold leading-none tracking-tight", className)} {...props}>
      {children}
    </h2>
  )
);
DialogTitle.displayName = "DialogTitle";

const DialogDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, children, ...props }, ref) => (
    <p ref={ref} className={cn("text-sm text-muted-foreground", className)} {...props}>
      {children}
    </p>
  )
);
DialogDescription.displayName = "DialogDescription";

const DialogFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...props }, ref) => (
    <div ref={ref} className={cn("flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2", className)} {...props}>
      {children}
    </div>
  )
);
DialogFooter.displayName = "DialogFooter";

export { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter };