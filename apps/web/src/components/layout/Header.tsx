"use client";

import * as React from "react";
import {
  Search,
  Bell,
  Inbox,
  HelpCircle,
  ChevronDown,
  User,
  Settings,
  LogOut,
  Moon,
  Sun,
  LayoutDashboard,
  Plus,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { APP_NAME, SUPPORTED_LOCALES, VIEW_LABELS } from "@/lib/constants";
import { getInitials } from "@/lib/utils";
import { useTheme } from "next-themes";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { useCreateProjectSheet } from "@/components/projects/CreateProjectSheetProvider";

interface HeaderProps {
  onSearchOpen?: () => void;
}

export function Header({ onSearchOpen }: HeaderProps) {
  const { theme, setTheme } = useTheme();
  const { user, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const { openCreateProject } = useCreateProjectSheet();
  const [mounted, setMounted] = React.useState(false);

  React.useEffect(() => setMounted(true), []);

  const displayName = user?.fullName ?? "User";
  const displayRole = user?.roles[0]?.name ?? user?.jobTitle ?? "";
  const displayEmail = user?.email ?? "";

  async function handleSignOut() {
    await logout();
    const locale = pathname.split("/")[1] ?? "en";
    router.push(`/${locale}/login`);
  }

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80 px-4 lg:px-6 relative before:absolute before:inset-x-0 before:-bottom-px before:h-px before:bg-gradient-to-r before:from-transparent before:via-primary/15 before:to-transparent">
      <div className="flex items-center gap-2 lg:hidden">
        <div className="relative flex h-8 w-8 items-center justify-center rounded-xl bg-primary-gradient text-[11px] font-bold text-white shadow-primary-sm">
          N
        </div>
        <span className="text-sm font-bold text-foreground">{APP_NAME}</span>
      </div>

      <div className="flex flex-1 items-center gap-3">
        <button
          onClick={onSearchOpen}
          className={cn(
            "group flex h-9 w-full max-w-[400px] items-center gap-2 rounded-lg border bg-muted/40 px-3 text-sm text-muted-foreground/70 transition-all duration-200",
            "hover:border-primary/30 hover:bg-muted/60 hover:text-muted-foreground",
            "focus-visible:border-primary focus-visible:ring-2 focus-visible:ring-primary/20"
          )}
        >
          <Search className="h-4 w-4 shrink-0" />
          <span className="flex-1 text-left">Search projects, tasks, documents...</span>
          <kbd className="hidden rounded-md border bg-background px-2 py-0.5 text-[10px] font-medium text-muted-foreground/60 md:inline-flex items-center gap-1">
            <span className="text-[9px]">⌘</span>K
          </kbd>
        </button>
      </div>

      <div className="flex items-center gap-1">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" size="sm" className="hidden md:inline-flex gap-1.5 h-9 text-xs font-medium px-3">
              <Plus className="h-3.5 w-3.5" />
              <span>Create</span>
              <ChevronDown className="h-3 w-3 text-muted-foreground/60" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56 p-1">
            <DropdownMenuLabel className="px-2 py-1 text-xs font-medium text-muted-foreground">Create new</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm" onSelect={openCreateProject}>
              <LayoutDashboard className="mr-2 h-4 w-4" />
              Project
            </DropdownMenuItem>
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm">
              <Plus className="mr-2 h-4 w-4" />
              Task
            </DropdownMenuItem>
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm">
              <User className="mr-2 h-4 w-4" />
              Team Member
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm text-muted-foreground">
              <HelpCircle className="mr-2 h-4 w-4" />
              Import MS Project...
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <div className="mx-2 h-6 w-px bg-border" />

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="gap-1.5 h-9 text-xs font-medium text-muted-foreground px-3">
              <LayoutDashboard className="h-4 w-4" />
              <span className="hidden md:inline">Executive</span>
              <ChevronDown className="h-3 w-3 text-muted-foreground/50" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48 p-1">
            <DropdownMenuLabel className="px-2 py-1 text-xs font-medium text-muted-foreground">Switch View</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {Object.entries(VIEW_LABELS).map(([key, label]) => (
              <DropdownMenuItem key={key} className="rounded-md px-2 py-1.5 text-sm">
                <LayoutDashboard className="mr-2 h-4 w-4 text-muted-foreground" />
                {label}
                {key === "executive" && <DropdownMenuShortcut className="text-xs text-muted-foreground">✓</DropdownMenuShortcut>}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

        {SUPPORTED_LOCALES.length > 1 && <LanguageSwitcher />}

        {mounted && (
          <Button
            variant="ghost"
            size="icon"
            className="text-muted-foreground h-9 w-9"
            onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
          >
            {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
          </Button>
        )}

        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground h-9 w-9"
        >
          <HelpCircle className="h-4 w-4" />
        </Button>

        <Button
          variant="ghost"
          size="icon"
          className="relative text-muted-foreground h-9 w-9"
          asChild
        >
          <a href="/notifications">
            <Bell className="h-4 w-4" />
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[14px] items-center justify-center rounded-full bg-destructive px-1 text-[9px] font-bold text-destructive-foreground">
              12
            </span>
          </a>
        </Button>

        <Button
          variant="ghost"
          size="icon"
          className="relative text-muted-foreground h-9 w-9"
          asChild
        >
          <a href="/my-work/approvals">
            <Inbox className="h-4 w-4" />
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[14px] items-center justify-center rounded-full bg-warning px-1 text-[9px] font-bold text-warning-foreground">
              5
            </span>
          </a>
        </Button>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="gap-2 h-9 px-3">
              <Avatar className="h-7 w-7">
                <AvatarFallback className="text-[10px] font-medium bg-primary/10 text-primary">{getInitials(displayName)}</AvatarFallback>
              </Avatar>
              <div className="hidden text-left md:block">
                <p className="text-xs font-medium leading-tight text-foreground">{displayName}</p>
                <p className="text-[10px] leading-tight text-muted-foreground">{displayRole}</p>
              </div>
              <ChevronDown className="h-3 w-3 text-muted-foreground/50" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56 p-1">
            <DropdownMenuLabel className="px-2 py-1 font-normal">
              <div className="flex flex-col">
                <span className="text-sm font-medium text-foreground">{displayName}</span>
                <span className="text-xs text-muted-foreground">{displayEmail}</span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm">
                <User className="mr-2 h-4 w-4" />
                Profile
              </DropdownMenuItem>
              <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm">
                <Settings className="mr-2 h-4 w-4" />
                Account Settings
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm text-destructive focus:text-destructive" onSelect={handleSignOut}>
              <LogOut className="mr-2 h-4 w-4" />
              Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}