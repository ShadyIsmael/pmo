"use client";

import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { CreateRiskForm } from "@/components/risks/CreateRiskForm";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { Card, CardContent } from "@/components/ui/card";

export default function CreateRiskPage() {
  const t = useTranslations("risks");
  const { user } = useAuth();
  const canCreateRisk = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Create));
  const locale = useLocale();
  const searchParams = useSearchParams();
  const projectId = searchParams.get("projectId") ?? undefined;
  const returnTo = projectId ? `/${locale}/projects/${encodeURIComponent(projectId)}?tab=risks-issues` : `/${locale}/risks-issues/risks`;

  return <div className="space-y-6"><PageHeader title={t("createTitle")} description={t("createDescription")} />{canCreateRisk ? <CreateRiskForm initialProjectId={projectId} returnTo={returnTo} /> : <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("notAuthorized")}</CardContent></Card>}</div>;
}
