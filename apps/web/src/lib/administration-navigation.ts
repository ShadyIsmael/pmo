import { Building2, GitBranch, ListFilter, Plug, Settings2, Shield, type LucideIcon } from "lucide-react";
import { Permissions } from "@/lib/permissions";

export type AdministrationCategory = {
  id: string;
  href: string;
  labelKey: string;
  icon: LucideIcon;
  requiredPermissions: string[];
};

/** Single source of truth for the compact Administration category menu. */
export const administrationCategoryConfig: AdministrationCategory[] = [
  { id: "organization", href: "/administration/organization", labelKey: "organization", icon: Building2, requiredPermissions: [Permissions.Settings.Read] },
  { id: "access", href: "/administration/access", labelKey: "accessSecurity", icon: Shield, requiredPermissions: [Permissions.Users.Read, Permissions.Settings.Read] },
  { id: "reference-data", href: "/administration/reference-data", labelKey: "referenceData", icon: ListFilter, requiredPermissions: [Permissions.Settings.Read] },
  { id: "platform-settings", href: "/administration/settings", labelKey: "platformSettings", icon: Settings2, requiredPermissions: [Permissions.Settings.Read] },
  { id: "integrations", href: "/administration/integrations", labelKey: "integrations", icon: Plug, requiredPermissions: [Permissions.Settings.Read] },
  { id: "governance", href: "/administration/governance", labelKey: "governance", icon: GitBranch, requiredPermissions: [Permissions.Settings.Read, Permissions.Workflow.View] },
];
