"use client";

import * as React from "react";
import { Building2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { useAuth } from "@/lib/auth";

interface CurrentOrganizationIndicatorProps {
  className?: string;
}

export function CurrentOrganizationIndicator({ className }: CurrentOrganizationIndicatorProps) {
  const t = useTranslations("organization");
  const { status, user } = useAuth();
  const tenantId = user?.tenantId;
  const label = fallbackTenantLabel(tenantId, t("unknownOrganization"));
  const isLoading = status === "loading";
  const accessibleLabel = isLoading ? t("loadingOrganization") : `${t("currentOrganization")}: ${label}`;

  return (
    <div
      className={cn("flex min-w-0 items-center gap-1.5 rounded-md px-1.5 py-1 text-start", className)}
      aria-label={accessibleLabel}
      title={isLoading ? t("loadingOrganization") : accessibleLabel}
    >
      <Building2 className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
      <span className="hidden text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground sm:inline">
        {t("organization")}
      </span>
      {isLoading ? (
        <span className="h-3.5 w-20 animate-pulse rounded bg-muted" aria-hidden="true" />
      ) : (
        <span className="max-w-[7rem] truncate text-xs font-semibold text-foreground sm:max-w-[10rem]">
          {label}
        </span>
      )}
    </div>
  );
}

function fallbackTenantLabel(tenantId: string | undefined, unknownLabel: string): string {
  if (!tenantId) return unknownLabel;
  const compactId = tenantId.replace(/-/g, "").slice(0, 8);
  return compactId ? `· ${compactId}` : unknownLabel;
}
