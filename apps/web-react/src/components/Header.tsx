import * as React from "react";
import { Link, useNavigate } from "react-router-dom";
import { Bell, Building2, Check, ChevronDown, Eye, Languages, LogOut, Moon, Search, Settings, Sun, User } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { useTheme } from "@theme/ThemeProvider";
import { useUnreadNotificationCount } from "@api/notifications";
import { useManagement } from "@api/management";
import { MANAGEMENT_PERSPECTIVES } from "@lib/perspective";
import { HeaderTimer } from "./HeaderTimer";
import { Button } from "./ui/Button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuGroup, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "./ui/DropdownMenu";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/Select";
import { Badge } from "./ui/Badge";

function getInitials(name: string): string {
  return name.split(" ").map((p) => p[0]).join("").slice(0, 2).toUpperCase() || "US";
}

function CurrentOrganizationIndicator() {
  const { t } = useI18n();
  const { user, status } = useAuth();
  const label = user?.tenantId ? `· ${user.tenantId.replace(/-/g, "").slice(0, 8)}` : t("organization.unknownOrganization");
  if (status === "loading") return <span className="h-3.5 w-20 animate-pulse rounded bg-muted" aria-hidden="true" />;
  return (
    <div className="flex min-w-0 items-center gap-1.5 rounded-md px-1.5 py-1" aria-label={`${t("organization.currentOrganization")}: ${label}`} title={label}>
      <Building2 className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
      <span className="max-w-[7rem] truncate text-xs font-semibold sm:max-w-[10rem]">{label}</span>
    </div>
  );
}

function PerspectiveSwitcher() {
  const { t } = useI18n();
  const { context, snapshot, effectiveReadOnly, switchPerspective, exitViewAs } = useManagement();
  if (!context || !snapshot || snapshot.availablePerspectives.length === 0) return null;
  const selectedId =
    context.perspective === MANAGEMENT_PERSPECTIVES.PortfolioManager ? context.portfolioId :
    context.perspective === MANAGEMENT_PERSPECTIVES.ProgramManager ? context.programId : context.projectId;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" className="hidden h-8 gap-1.5 px-2.5 text-xs font-medium md:inline-flex">
          <Eye className="h-3.5 w-3.5" />
          <span>{t("perspective.viewingAs")}: {t(`perspective.${context.perspective}`)}</span>
          {effectiveReadOnly ? <Badge status="ReadOnly">{t("perspective.readOnly")}</Badge> : null}
          <ChevronDown className="h-3 w-3 text-muted-foreground/60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel>{t("perspective.nativeView")}</DropdownMenuLabel>
        {snapshot.availablePerspectives.filter((a) => a.isNative).map((a) => (
          a.contexts.length > 1 ? (
            <div key={a.perspective} className="space-y-1 px-2 py-1.5">
              <p className="text-xs font-medium text-muted-foreground">{t(`perspective.${a.perspective}`)}</p>
              <Select value={context.perspective === a.perspective ? selectedId : ""} onValueChange={(id) => switchPerspective(a.perspective, id)}>
                <SelectTrigger><SelectValue placeholder={t(`perspective.${a.perspective}`)} /></SelectTrigger>
                <SelectContent>
                  {a.contexts.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}{c.code ? ` · ${c.code}` : ""}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          ) : (
            <DropdownMenuItem key={a.perspective} onSelect={() => switchPerspective(a.perspective)}>
              <span className="flex-1">{t(`perspective.${a.perspective}`)}</span>
              {context.perspective === a.perspective && !effectiveReadOnly ? <Check className="h-4 w-4 text-primary" /> : null}
            </DropdownMenuItem>
          )
        ))}
        {effectiveReadOnly ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={exitViewAs}>{t("perspective.exitView")}</DropdownMenuItem>
          </>
        ) : null}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function Header() {
  const { user, logout } = useAuth();
  const { locale, setLocale, t } = useI18n();
  const { theme, setTheme } = useTheme();
  const { canMutate, effectiveReadOnly } = useManagement();
  const unread = useUnreadNotificationCount(Boolean(user));
  const navigate = useNavigate();
  const [mounted, setMounted] = React.useState(false);
  React.useEffect(() => setMounted(true), []);

  const displayName = user?.fullName ?? "User";
  const displayRole = user?.roles?.[0]?.name ?? user?.jobTitle ?? "";
  const displayEmail = user?.email ?? "";
  const showGlobalCreate = canMutate && !effectiveReadOnly;

  async function handleSignOut() {
    await logout();
    navigate("/login", { replace: true });
  }

  return (
    <header className="nexus-topbar sticky top-0 z-30 flex h-12 items-center gap-3 border-b bg-header/95 px-4 backdrop-blur lg:px-6">
      <div className="flex flex-1 items-center gap-3">
        <button
          type="button"
          onClick={() => window.dispatchEvent(new CustomEvent("nexus:open-global-search"))}
          className="group flex h-9 w-full max-w-[300px] min-w-0 items-center gap-2 rounded-lg border bg-muted/30 px-3 text-sm text-muted-foreground/70 hover:border-primary/30 hover:bg-muted/50"
        >
          <Search className="h-3.5 w-3.5 shrink-0" />
          <span className="min-w-0 flex-1 truncate text-left text-xs">{t("nav.searchPlaceholder")}</span>
          <kbd className="hidden rounded border bg-background px-1.5 py-0.5 text-[10px] md:inline-flex">⌘K</kbd>
        </button>
        <HeaderTimer />
      </div>

      <div className="flex items-center gap-1">
        {showGlobalCreate ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="hidden h-8 gap-1.5 px-2.5 text-xs font-medium md:inline-flex">
                <span>+</span>
                <span>{t("common.create")}</span>
                <ChevronDown className="h-3 w-3 text-muted-foreground/60" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              <DropdownMenuLabel>{t("common.create")}</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => navigate("/projects")}>{t("nav.projects")}</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => navigate("/my-work/tasks")}>{t("nav.tasks")}</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => navigate("/resources")}>{t("nav.peopleResources")}</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}

        <div className="mx-1.5 h-5 w-px bg-border" />
        <CurrentOrganizationIndicator />
        <div className="mx-1.5 h-5 w-px bg-border" />
        <PerspectiveSwitcher />

        <button
          type="button"
          onClick={() => setLocale(locale === "en" ? "ar" : "en")}
          aria-label={t("nav.language")}
          className="icon-button"
        >
          <Languages size={17} />
        </button>

        {mounted ? (
          <button
            type="button"
            onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
            aria-label={theme === "dark" ? t("nav.lightTheme") : t("nav.darkTheme")}
            className="icon-button"
          >
            {theme === "dark" ? <Sun size={17} /> : <Moon size={17} />}
          </button>
        ) : null}

        <Link
          to="/notifications"
          className="icon-button relative"
          aria-label={`${unread.data ?? 0} ${t("nav.notifications")}`}
        >
          <Bell size={17} />
          {(unread.data ?? 0) > 0 ? (
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[14px] items-center justify-center rounded-full bg-destructive px-1 text-[9px] font-bold text-destructive-foreground">
              {unread.data}
            </span>
          ) : null}
        </Link>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 gap-2 px-2.5">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-[10px] font-medium text-primary">
                {getInitials(displayName)}
              </span>
              <span className="hidden text-left md:block">
                <span className="block text-xs font-medium leading-tight">{displayName}</span>
                <span className="block text-[10px] leading-tight text-muted-foreground">{displayRole}</span>
              </span>
              <ChevronDown className="h-3 w-3 text-muted-foreground/50" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52">
            <DropdownMenuLabel>
              <span className="block text-sm font-medium">{displayName}</span>
              <span className="block text-xs font-normal text-muted-foreground">{displayEmail}</span>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem onSelect={() => navigate("/account/profile")}>
                <User className="me-2 h-4 w-4" />{t("account.menu.profile")}
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => navigate("/account/preferences")}>
                <Settings className="me-2 h-4 w-4" />{t("account.menu.preferences")}
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => void handleSignOut()} className="text-destructive">
              <LogOut className="me-2 h-4 w-4" />{t("nav.signOut")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
