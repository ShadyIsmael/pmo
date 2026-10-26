import * as React from "react";
import { useAuth } from "./AuthProvider";
import { useI18n } from "../i18n/messages";
import { hasAnyPermission } from "@lib/permissions";

type RequireFreshPermissionProps = {
  permissions: string | string[];
  children: React.ReactNode;
};

type RefreshState = "checking" | "complete" | "failed";

/** Refreshes the persisted effective grants before guarding pages reached from an admin index. */
export function RequireFreshPermission({ permissions, children }: RequireFreshPermissionProps) {
  const { status, user, refreshUser } = useAuth();
  const { t } = useI18n();
  const [attempt, setAttempt] = React.useState(0);
  const [refreshState, setRefreshState] = React.useState<RefreshState>("checking");

  React.useEffect(() => {
    if (status !== "authenticated") {
      setRefreshState("checking");
      return;
    }

    let isCurrent = true;
    setRefreshState("checking");
    void refreshUser()
      .then(() => {
        if (isCurrent) setRefreshState("complete");
      })
      .catch(() => {
        if (isCurrent) setRefreshState("failed");
      });

    return () => {
      isCurrent = false;
    };
  }, [attempt, refreshUser, status]);

  if (status !== "authenticated" || refreshState === "checking") {
    return <div className="route-state" role="status">{t("auth.loadingAuthorization")}</div>;
  }

  if (refreshState === "failed") {
    return (
      <div className="route-state" role="alert">
        <p>{t("auth.permissionRefreshFailed")}</p>
        <div className="route-state-actions">
          <button type="button" onClick={() => setAttempt((current) => current + 1)}>{t("common.retry")}</button>
        </div>
      </div>
    );
  }

  const required = Array.isArray(permissions) ? permissions : [permissions];
  if (!user || !hasAnyPermission(user.permissions ?? [], required)) {
    return <div className="route-state" role="alert">{t("auth.accessDenied")}</div>;
  }

  return <>{children}</>;
}
