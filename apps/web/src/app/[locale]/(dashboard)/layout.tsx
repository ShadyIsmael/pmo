"use client";

import * as React from "react";
import { useRouter, usePathname } from "next/navigation";
import { Loader2 } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { Sidebar } from "@/components/layout/Sidebar";
import { Header } from "@/components/layout/Header";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";
import { CreateProjectSheetProvider } from "@/components/projects/CreateProjectSheetProvider";
import dynamic from "next/dynamic";

const CommandPalette = dynamic(
  () => import("@/components/layout/CommandPalette").then((m) => m.CommandPalette),
  { ssr: false, loading: () => null }
);

const COLLAPSE_STORAGE_KEY = "nexus_sidebar_collapsed";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [sidebarCollapsed, setSidebarCollapsed] = React.useState(false);
  const [commandOpen, setCommandOpen] = React.useState(false);
  const { status } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  React.useEffect(() => {
    if (status === "unauthenticated") {
      const locale = pathname.split("/")[1] ?? "en";
      router.replace(`/${locale}/login`);
    }
  }, [status, pathname, router]);

  React.useEffect(() => {
    try {
      if (window.localStorage.getItem(COLLAPSE_STORAGE_KEY) === "1") {
        setSidebarCollapsed(true);
      }
    } catch {
      // Storage is best-effort.
    }
  }, []);

  React.useEffect(() => {
    try {
      window.localStorage.setItem(COLLAPSE_STORAGE_KEY, sidebarCollapsed ? "1" : "0");
    } catch {
      // Storage is best-effort.
    }
  }, [sidebarCollapsed]);

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setCommandOpen((open) => !open);
      }
    };
    document.addEventListener("keydown", down);
    return () => document.removeEventListener("keydown", down);
  }, []);

  if (status !== "authenticated") {
    return (
      <div className="flex h-screen items-center justify-center bg-muted/30">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <CreateProjectSheetProvider>
      <div className="flex h-screen overflow-hidden bg-muted/30">
      <Sidebar
        collapsed={sidebarCollapsed}
        onToggle={() => setSidebarCollapsed((collapsed) => !collapsed)}
      />
      <div className="flex flex-1 flex-col overflow-hidden">
        <Header onSearchOpen={() => setCommandOpen(true)} />
        <main className="flex-1 overflow-auto bg-subtle-gradient">
          <div className="flex flex-col gap-4 p-4 lg:p-5 xl:p-6">
            <Breadcrumbs />
            {children}
          </div>
        </main>
      </div>

      {commandOpen && <CommandPalette open={commandOpen} onOpenChange={setCommandOpen} />}
      </div>
    </CreateProjectSheetProvider>
  );
}
