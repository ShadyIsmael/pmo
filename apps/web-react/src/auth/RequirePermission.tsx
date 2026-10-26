import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission } from "@lib/permissions";
import { useI18n } from "../i18n/messages";

export function RequirePermission({ permissions, children }: { permissions: string | string[]; children: ReactNode }) {
  const { user, status } = useAuth();
  const { t } = useI18n();
  if (status !== "authenticated") return <div className="route-state" role="status">{t("auth.loadingAuthorization")}</div>;
  const required = Array.isArray(permissions) ? permissions : [permissions];
  if (!user || !hasAnyPermission(user.permissions ?? [], required)) {
    return <div className="route-state" role="alert">{t("auth.accessDenied")}</div>;
  }
  return <>{children}</>;
}

export function RedirectToHome() {
  return <Navigate to="/home" replace />;
}
