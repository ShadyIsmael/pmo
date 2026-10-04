"use client";

import { FileSearch, GitBranch } from "lucide-react";
import { useTranslations } from "next-intl";
import { AdministrationAreaPage } from "@/components/administration/AdministrationAreaPage";
import { Permissions } from "@/lib/permissions";

export default function AdministrationGovernancePage() {
  const t = useTranslations("administration.areas.governance");
  return <AdministrationAreaPage
    title={t("title")}
    description={t("description")}
    items={[
      { id: "workflows", href: "/workflows", icon: GitBranch, title: t("workflows.title"), description: t("workflows.description"), requiredPermissions: [Permissions.Workflow.View] },
      { id: "audit-log", href: "/security-audit/audit-log", icon: FileSearch, title: t("auditLog.title"), description: t("auditLog.description"), requiredPermissions: [Permissions.Settings.Read] },
    ]}
  />;
}
