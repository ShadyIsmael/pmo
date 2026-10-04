"use client";

import { Shield, UsersRound } from "lucide-react";
import { useTranslations } from "next-intl";
import { AdministrationAreaPage } from "@/components/administration/AdministrationAreaPage";
import { Permissions } from "@/lib/permissions";

export default function AdministrationAccessPage() {
  const t = useTranslations("administration.areas.access");
  return <AdministrationAreaPage
    title={t("title")}
    description={t("description")}
    items={[
      { id: "users", href: "/administration/users", icon: UsersRound, title: t("users.title"), description: t("users.description"), requiredPermissions: [Permissions.Users.Read] },
      { id: "roles", href: "/administration/roles", icon: Shield, title: t("roles.title"), description: t("roles.description"), requiredPermissions: [Permissions.Settings.Read] },
    ]}
  />;
}
