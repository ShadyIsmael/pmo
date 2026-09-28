import type { PlanDependencyType } from "@/lib/api/planning";
import { PLANNING_DEPENDENCY_TYPE } from "@/lib/enums";

const SHORT_TO_TYPE = { FS: PLANNING_DEPENDENCY_TYPE.finishToStart, SS: PLANNING_DEPENDENCY_TYPE.startToStart, FF: PLANNING_DEPENDENCY_TYPE.finishToFinish, SF: PLANNING_DEPENDENCY_TYPE.startToFinish } as const satisfies Record<string, PlanDependencyType>;
const TYPE_TO_SHORT: Record<PlanDependencyType, keyof typeof SHORT_TO_TYPE> = { FinishToStart: "FS", StartToStart: "SS", FinishToFinish: "FF", StartToFinish: "SF" };

export interface ParsedPredecessor { rowNumber: number; type: PlanDependencyType; lag?: number; }

export function parsePredecessor(value: string): ParsedPredecessor | undefined {
  const match = /^(\d+)(FS|SS|FF|SF)?(?:([+-]\d+)d?)?$/i.exec(value.trim());
  if (!match) return undefined;
  const rowNumber = Number(match[1]);
  if (!Number.isSafeInteger(rowNumber) || rowNumber < 1) return undefined;
  const candidate = match[2]?.toUpperCase();
  const shortType: keyof typeof SHORT_TO_TYPE = candidate === "SS" || candidate === "FF" || candidate === "SF" ? candidate : "FS";
  const lag = match[3] === undefined ? undefined : Number(match[3]);
  return { rowNumber, type: SHORT_TO_TYPE[shortType], lag };
}

export function formatPredecessor(rowNumber: number, type: PlanDependencyType, lag?: number) {
  const lagText = lag ? `${lag > 0 ? "+" : ""}${lag}d` : "";
  return `${rowNumber}${TYPE_TO_SHORT[type]}${lagText}`;
}
