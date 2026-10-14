import * as React from "react";
import { cn } from "@lib/utils";

export function Checkbox({
  checked,
  onCheckedChange,
  id,
  className,
  ...props
}: {
  checked?: boolean;
  onCheckedChange?: () => void;
  id?: string;
  className?: string;
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, "checked" | "onChange">) {
  return (
    <input
      type="checkbox"
      id={id}
      checked={checked}
      onChange={onCheckedChange}
      className={cn(
        "h-4 w-4 shrink-0 rounded-sm border border-primary ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 accent-primary",
        className
      )}
      {...props}
    />
  );
}
