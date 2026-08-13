import * as React from "react";
import { cn } from "@/lib/utils";

function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn("animate-pulse rounded-lg bg-gradient-to-r from-muted/50 via-muted/80 to-muted/50 bg-[length:200%_100%] motion-safe:animate-[shimmer_1.5s_ease-in-out_infinite]", className)}
      {...props}
    />
  );
}

export { Skeleton };
