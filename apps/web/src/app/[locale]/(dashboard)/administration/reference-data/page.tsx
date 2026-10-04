"use client";

import { Coins, ListFilter, Tag, UsersRound } from "lucide-react";
import { useTranslations } from "next-intl";
import { AdministrationAreaPage } from "@/components/administration/AdministrationAreaPage";
import { Permissions } from "@/lib/permissions";

export default function AdministrationReferenceDataPage() {
  const t = useTranslations("administration.areas.referenceData");
  const settingsAccess = [Permissions.Settings.Read];
  return <AdministrationAreaPage
    title={t("title")}
    description={t("description")}
    items={[
      { id: "project-roles", href: "/administration/reference-data/project-roles", icon: UsersRound, title: t("projectRoles.title"), description: t("projectRoles.description"), requiredPermissions: settingsAccess },
      { id: "vendor-categories", href: "/administration/reference-data/vendor-categories", icon: Tag, title: t("vendorCategories.title"), description: t("vendorCategories.description"), requiredPermissions: settingsAccess },
      { id: "work-item-statuses", href: "/administration/reference-data/work-item-statuses", icon: ListFilter, title: t("workItemStatuses.title"), description: t("workItemStatuses.description"), requiredPermissions: settingsAccess },
      { id: "currencies", href: "/administration/currencies", icon: Coins, title: t("currencies.title"), description: t("currencies.description"), requiredPermissions: settingsAccess },
    ]}
  />;
}
