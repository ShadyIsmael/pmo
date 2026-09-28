"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import type { NavItem } from "@/lib/navigation";
import { itemIsActive } from "./sidebar-utils";

export function SidebarNavItem({
  item,
  pathname,
  onNavigate,
}: {
  item: NavItem;
  pathname: string;
  onNavigate?: () => void;
}) {
  const t = useTranslations("nav");
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;

  if (item.items && item.items.length > 0) {
    return (
      <div className="space-y-px">
        <SidebarNavItem item={{ ...item, items: undefined, href: item.href }} pathname={pathname} onNavigate={onNavigate} />
        {item.items.map((subItem) => (
          <SidebarNavItem key={subItem.id} item={subItem} pathname={pathname} onNavigate={onNavigate} />
        ))}
      </div>
    );
  }

  return (
    <Link
      href={item.href}
      onClick={onNavigate}
      className={cn(
        "group relative flex items-center gap-2 rounded-md px-2 py-1.5 text-[12px] font-medium transition-all duration-150",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        active
          ? "bg-primary/10 text-primary"
          : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
      )}
    >
      {active && <span className="absolute inset-y-1 start-0 w-[3px] rounded-full bg-primary" />}
      <Icon className={cn("h-3.5 w-3.5 shrink-0", active ? "text-primary" : "text-muted-foreground/60")} />
      <span>{t(item.labelKey)}</span>
    </Link>
  );
}
