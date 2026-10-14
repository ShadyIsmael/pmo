import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission } from "@lib/permissions";

export function RequirePermission({ permissions, children }: { permissions: string | string[]; children: ReactNode }) {
  const { user, status } = useAuth();
  if (status === "loading") return <p className="py-12 text-center text-sm text-muted-foreground">Loading...</p>;
  const required = Array.isArray(permissions) ? permissions : [permissions];
  if (!user || !hasAnyPermission(user.permissions ?? [], required)) {
    return <p className="py-12 text-center text-sm text-muted-foreground">You do not have access to this area.</p>;
  }
  return <>{children}</>;
}

export function RedirectToHome() {
  return <Navigate to="/home" replace />;
}
