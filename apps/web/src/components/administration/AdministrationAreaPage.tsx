"use client";

import Link from "next/link";
import { ArrowUpRight, type LucideIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/PageHeader";
import { useAuth } from "@/lib/auth";
import { hasAnyPermission } from "@/lib/permissions";

export type AdministrationAreaItem = {
  id: string;
  href: string;
  icon: LucideIcon;
  title: string;
  description: string;
  requiredPermissions?: string[];
};

export function AdministrationAreaPage({
  title,
  description,
  items,
  emptyMessage,
}: {
  title: string;
  description: string;
  items: AdministrationAreaItem[];
  emptyMessage?: string;
}) {
  const t = useTranslations("administration.home");
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const visibleItems = items.filter((item) => !item.requiredPermissions?.length || hasAnyPermission(permissions, item.requiredPermissions));

  return (
    <div className="space-y-6">
      <PageHeader title={title} description={description} />
      {visibleItems.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2">
          {visibleItems.map(({ id, href, icon: Icon, title: itemTitle, description: itemDescription }) => (
            <Link key={id} href={href} className="group">
              <Card className="h-full border-border/70 shadow-sm transition-all duration-200 group-hover:-translate-y-0.5 group-hover:border-primary/30 group-hover:shadow-md">
                <CardHeader className="gap-4">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <CardTitle className="text-base">{itemTitle}</CardTitle>
                    <CardDescription className="mt-1.5 text-xs leading-5">{itemDescription}</CardDescription>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary">
                    <span>{t("open")}</span>
                    <ArrowUpRight className="h-4 w-4 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5" />
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      ) : (
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">{emptyMessage ?? t("noAccessibleAreas")}</CardContent>
        </Card>
      )}
    </div>
  );
}
