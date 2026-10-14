import { useI18n } from "@i18n/messages";
import { getRiskLevelTextClass } from "@lib/status-presentation";
import type { HomeRiskSummary } from "@api/home";

export function TopRisksWidget({ risks }: { risks: HomeRiskSummary[] }) {
  const { t } = useI18n();

  if (!risks.length) {
    return <div className="py-8 text-center text-sm text-muted-foreground">{t("dashboard.noRisks")}</div>;
  }

  return (
    <div className="space-y-3">
      {risks.slice(0, 5).map((risk) => (
        <div key={risk.id} className="rounded-lg border bg-muted/30 p-3">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{risk.title}</p>
              <p className="text-xs text-muted-foreground">{risk.projectName}</p>
            </div>
            <span className={`text-xs font-medium ${getRiskLevelTextClass(risk.riskLevel)}`}>
              {risk.riskLevel}
            </span>
          </div>
        </div>
      ))}
    </div>
  );
}
