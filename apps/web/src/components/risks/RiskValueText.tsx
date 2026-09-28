"use client";

import { useTranslations } from "next-intl";
import type { RiskImpactType, RiskLevel, RiskStatus } from "@/lib/api/risks";
import { RiskImpactTypeHelper, RiskLevelHelper, RiskStatusHelper } from "@/lib/enums";
import { getRiskImpactTypeAccent, riskImpactTypeAccents, riskLevelAccents, riskStatusAccents } from "@/lib/risk-presentation";
import { cn } from "@/lib/utils";

type RiskValueTextProps = {
  value?: string | null;
  className?: string;
};

export function RiskLevelText({ value, className }: RiskValueTextProps) {
  const enums = useTranslations("enums");
  const labelKey = RiskLevelHelper.getLabelKey(value);

  return <span className={cn(RiskLevelHelper.getTextClass(value), riskLevelAccents[value as RiskLevel]?.chip, "font-semibold", className)}>{labelKey ? enums(labelKey) : "—"}</span>;
}

export function RiskStatusText({ value, className }: RiskValueTextProps) {
  const enums = useTranslations("enums");
  const labelKey = RiskStatusHelper.getLabelKey(value);

  return <span className={cn(RiskStatusHelper.getTextClass(value), riskStatusAccents[value as RiskStatus]?.chip, "font-semibold", className)}>{labelKey ? enums(labelKey) : "—"}</span>;
}

export function RiskImpactTypeText({ value, className }: { value?: RiskImpactType | string | null; className?: string }) {
  const enums = useTranslations("enums");
  const accent = getRiskImpactTypeAccent(value);
  const knownType = value && Object.prototype.hasOwnProperty.call(riskImpactTypeAccents, value) ? value as RiskImpactType : undefined;
  const labelKey = knownType ? RiskImpactTypeHelper.getLabelKey(knownType) : value ? RiskImpactTypeHelper.getLabelKey("Other") : undefined;

  return (
    <span className={cn("inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] font-medium", accent.chip, className)}>
      {value ? <span aria-hidden="true" className={cn("h-1.5 w-1.5 shrink-0 rounded-full", accent.dot)} /> : null}
      {labelKey ? enums(labelKey) : "—"}
    </span>
  );
}
