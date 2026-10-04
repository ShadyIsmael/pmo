import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./AuthProvider";
import { useI18n } from "../i18n/messages";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { status } = useAuth();
  const location = useLocation();
  const { t } = useI18n();
  if (status === "loading") return <div className="route-state" role="status">{t("loading")}</div>;
  if (status === "unauthenticated") return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
}
