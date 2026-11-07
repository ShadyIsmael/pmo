import { useParams } from "react-router-dom";
import { ApiError } from "@api/client";
import { useI18n } from "@i18n/messages";
import { useRiskActionFollowUpTarget } from "./api/risks";
import { RiskDetailPage } from "./RiskDetailPage";

export function RiskActionFollowUpPage() {
  const { actionPublicId = "" } = useParams<{ actionPublicId: string }>();
  const { t } = useI18n();
  const target = useRiskActionFollowUpTarget(actionPublicId, Boolean(actionPublicId));

  if (target.isLoading) return <p role="status" className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;

  if (target.isError) {
    const status = target.error instanceof ApiError ? target.error.status : undefined;
    const message = status === 403 || status === 404 ? t("risks.notFound") : t("common.errorLoading");
    return <p role="alert" className="p-6 text-center text-sm text-destructive">{message}</p>;
  }

  const resolved = target.data;
  if (!resolved?.projectPublicId || !resolved.riskId || !resolved.actionId)
    return <p role="alert" className="p-6 text-center text-sm text-destructive">{t("risks.notFound")}</p>;

  return <RiskDetailPage
    riskIdOverride={resolved.riskId}
    actionIdOverride={resolved.actionId}
    projectPublicIdOverride={resolved.projectPublicId}
  />;
}
