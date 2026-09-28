"use client";

import * as React from "react";
import {
  Search,
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
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SUPPORTED_LOCALES } from "@/lib/constants";
import { getInitials, cn } from "@/lib/utils";
import { useTheme } from "next-themes";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { PerspectiveSwitcher } from "@/components/layout/PerspectiveSwitcher";
import { CurrentOrganizationIndicator } from "@/components/layout/CurrentOrganizationIndicator";
import { useManagementPerspective } from "@/lib/perspective-context";

interface LazyOverlayProps { open: boolean; onOpenChange: (_value: boolean) => void; }
type CreateProjectSheetComponent = React.ComponentType<LazyOverlayProps>;

export function HeaderInteractive({ staticActions }: { staticActions: React.ReactNode }) {
  const { theme, setTheme } = useTheme();
  const { user, logout } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagementPerspective();
  const showGlobalCreate = canMutate && !effectiveReadOnly;
  const router = useRouter();
  const pathname = usePathname();
  const [mounted, setMounted] = React.useState(false);
  const [createProjectOpen, setCreateProjectOpen] = React.useState(false);
  const [CreateProjectSheet, setCreateProjectSheet] = React.useState<CreateProjectSheetComponent | null>(null);

  React.useEffect(() => setMounted(true), []);

  const displayName = user?.fullName ?? "User";
  const displayRole = user?.roles[0]?.name ?? user?.jobTitle ?? "";
  const displayEmail = user?.email ?? "";

  const openCreateProject = React.useCallback(() => {
    setCreateProjectOpen(true);
    void import("@/components/projects/CreateProjectSheet")
      .then((module) => setCreateProjectSheet(() => module.CreateProjectSheet))
      .catch(() => setCreateProjectOpen(false));
  }, []);

  async function handleSignOut() {
    await logout();
    const locale = pathname.split("/")[1] ?? "en";
    router.push(`/${locale}/login`);
  }

  return (
    <>
      <div className="flex flex-1 items-center gap-3">
        <button
          onClick={() => window.dispatchEvent(new CustomEvent("nexus:open-global-search"))}
          className={cn(
            "group flex h-9 w-full max-w-[300px] min-w-0 items-center gap-2 rounded-lg border border-border bg-muted/30 px-3 text-sm text-muted-foreground/70 transition-all duration-150",
            "hover:border-primary/30 hover:bg-muted/50 hover:text-muted-foreground",
            "focus-visible:border-primary focus-visible:ring-2 focus-visible:ring-primary/20",
          )}
        >
          <Search className="h-3.5 w-3.5 shrink-0" />
          <span className="min-w-0 flex-1 truncate text-left text-xs">Search projects, tasks, documents...</span>
          <kbd className="hidden rounded border border-border bg-background px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground/60 md:inline-flex items-center gap-1">
            <span className="text-[9px]">⌘</span>K
          </kbd>
        </button>
      </div>

      <div className="flex items-center gap-1">
        {showGlobalCreate && <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" size="sm" className="hidden md:inline-flex gap-1.5 h-8 text-xs font-medium px-2.5">
              <Plus className="h-3.5 w-3.5" />
              <span>Create</span>
              <ChevronDown className="h-3 w-3 text-muted-foreground/60" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52 p-1">
            <DropdownMenuLabel>Create new</DropdownMenuLabel>
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
        </DropdownMenu>}

        <div className="mx-1.5 h-5 w-px bg-border" />

        <CurrentOrganizationIndicator />

        <div className="mx-1.5 h-5 w-px bg-border" />

        <PerspectiveSwitcher />

        {SUPPORTED_LOCALES.length > 1 && <LanguageSwitcher />}

        {mounted && (
          <Button variant="ghost" size="icon" className="text-muted-foreground h-8 w-8" onClick={() => setTheme(theme === "dark" ? "light" : "dark")}>
            {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
          </Button>
        )}

        {staticActions}

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="gap-2 h-8 px-2.5">
              <Avatar className="h-6 w-6">
                <AvatarFallback className="text-[10px] font-medium bg-primary/10 text-primary">{getInitials(displayName)}</AvatarFallback>
              </Avatar>
              <div className="hidden text-left md:block">
                <p className="text-xs font-medium leading-tight text-foreground">{displayName}</p>
                <p className="text-[10px] leading-tight text-muted-foreground">{displayRole}</p>
              </div>
              <ChevronDown className="h-3 w-3 text-muted-foreground/50" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52 p-1">
            <DropdownMenuLabel className="px-2 py-1 font-normal">
              <div className="flex flex-col">
                <span className="text-sm font-medium text-foreground">{displayName}</span>
                <span className="text-xs text-muted-foreground">{displayEmail}</span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm"><User className="mr-2 h-4 w-4" />Profile</DropdownMenuItem>
              <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm"><Settings className="mr-2 h-4 w-4" />Account Settings</DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="rounded-md px-2 py-1.5 text-sm text-destructive focus:text-destructive" onSelect={handleSignOut}>
              <LogOut className="mr-2 h-4 w-4" />Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {createProjectOpen && CreateProjectSheet && <CreateProjectSheet open={createProjectOpen} onOpenChange={setCreateProjectOpen} />}
    </>
  );
}
