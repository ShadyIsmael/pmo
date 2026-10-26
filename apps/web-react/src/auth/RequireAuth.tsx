import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./AuthProvider";
import { useI18n } from "../i18n/messages";
import { useManagement } from "../api/management";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { status, user } = useAuth();
  const location = useLocation();
  const { t } = useI18n();
  if (status === "unauthenticated") return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (status !== "authenticated" || !user) return <div className="route-state" role="status">{t("auth.loadingAuthorization")}</div>;
  if (user?.mustChangePassword && location.pathname !== "/account/change-initial-password") {
    return <Navigate to="/account/change-initial-password" replace />;
  }
  if (!user?.mustChangePassword && location.pathname === "/account/change-initial-password") {
    return <Navigate to="/home" replace />;
  }
  return <>{children}</>;
}

export function RequireManagementAuthorization({ children }: { children: React.ReactNode }) {
  const { loading, initializationError, retryInitialization } = useManagement();
  const { logout } = useAuth();
  const { t } = useI18n();

  if (loading) return <div className="route-state" role="status">{t("auth.loadingAuthorization")}</div>;
  if (initializationError) {
    return (
      <div className="route-state" role="alert">
        <p>{t("auth.authorizationInitializationFailed")}</p>
        <div className="route-state-actions">
          <button type="button" onClick={() => void retryInitialization()}>{t("common.retry")}</button>
          <button type="button" onClick={() => void logout()}>{t("auth.signOut")}</button>
        </div>
      </div>
    );
  }
  return <>{children}</>;
}
