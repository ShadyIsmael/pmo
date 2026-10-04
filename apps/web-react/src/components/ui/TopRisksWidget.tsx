import { cn } from "@lib/utils";
import { useI18n } from "@i18n/messages";
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
            <span className={cn("rounded px-2 py-0.5 text-xs font-medium",
              risk.riskLevel === "Critical" ? "bg-red-100 text-red-700" :
              risk.riskLevel === "High" ? "bg-orange-100 text-orange-700" :
              "bg-green-100 text-green-700"
            )}>
              {risk.riskLevel}
            </span>
          </div>
        </div>
      ))}
    </div>
  );
}
