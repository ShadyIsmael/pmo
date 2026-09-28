import * as React from "react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { cn, getInitials } from "@/lib/utils";

interface UserIdentityProps {
  name: string;
  secondary?: React.ReactNode;
  avatarUrl?: string | null;
  size?: "sm" | "md" | "lg";
  className?: string;
  nameClassName?: string;
}

const sizes = {
  sm: { avatar: "h-6 w-6", name: "text-xs", secondary: "text-[10px]" },
  md: { avatar: "h-8 w-8", name: "text-sm", secondary: "text-xs" },
  lg: { avatar: "h-10 w-10", name: "text-sm", secondary: "text-xs" },
} as const;

export function UserIdentity({ name, secondary, avatarUrl, size = "md", className, nameClassName }: UserIdentityProps) {
  const scale = sizes[size];
  return (
    <div className={cn("flex min-w-0 items-center gap-2", className)}>
      <Avatar className={cn(scale.avatar, "shrink-0 border border-border/70")}>
        {avatarUrl && <AvatarImage src={avatarUrl} alt="" />}
        <AvatarFallback className="bg-primary/10 font-semibold text-primary">{getInitials(name)}</AvatarFallback>
      </Avatar>
      <div className="min-w-0">
        <div className={cn("truncate font-medium text-foreground", scale.name, nameClassName)}>{name}</div>
        {secondary && <div className={cn("truncate text-muted-foreground", scale.secondary)}>{secondary}</div>}
      </div>
    </div>
  );
}