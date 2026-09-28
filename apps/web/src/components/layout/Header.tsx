import { APP_NAME } from "@/lib/constants";
import { Bell, HelpCircle, Inbox } from "lucide-react";
import { Button } from "@/components/ui/button";
import { HeaderInteractive } from "./HeaderInteractive";

function HeaderStaticActions() {
  return (
    <>
      <Button variant="ghost" size="icon" className="text-muted-foreground h-8 w-8">
        <HelpCircle className="h-4 w-4" />
      </Button>

      <Button variant="ghost" size="icon" className="relative text-muted-foreground h-8 w-8" asChild>
        <a href="/notifications">
          <Bell className="h-4 w-4" />
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[14px] items-center justify-center rounded-full bg-destructive px-1 text-[9px] font-bold text-destructive-foreground">12</span>
        </a>
      </Button>

      <Button variant="ghost" size="icon" className="relative text-muted-foreground h-8 w-8" asChild>
        <a href="/my-work/approvals">
          <Inbox className="h-4 w-4" />
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[14px] items-center justify-center rounded-full bg-warning px-1 text-[9px] font-bold text-warning-foreground">5</span>
        </a>
      </Button>
    </>
  );
}

export function Header() {
  return (
    <header className="nexus-topbar sticky top-0 z-30 flex h-12 items-center gap-3 border-b border-border bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80 px-4 lg:px-6">
      <div className="flex items-center gap-2 lg:hidden">
        <div className="relative flex h-7 w-7 items-center justify-center rounded-lg bg-primary-gradient text-[10px] font-bold text-white shadow-primary-sm">
          N
        </div>
        <span className="text-sm font-semibold text-foreground">{APP_NAME}</span>
      </div>
      <HeaderInteractive staticActions={<HeaderStaticActions />} />
    </header>
  );
}
