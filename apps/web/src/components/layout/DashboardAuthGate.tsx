"use client";

import * as React from "react";
import { usePathname, useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { useAuth } from "@/lib/auth";

export function DashboardAuthGate({ children }: { children: React.ReactNode }) {
  const { status } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  React.useEffect(() => {
    if (status !== "unauthenticated") return;
    const locale = pathname.split("/")[1] ?? "en";
    router.replace(`/${locale}/login`);
  }, [pathname, router, status]);

  if (status !== "authenticated") {
    return (
      <div className="flex h-screen items-center justify-center bg-muted/30">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return children;
}
