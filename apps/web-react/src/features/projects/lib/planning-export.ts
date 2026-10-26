import type {
  PlanActivityDto,
  PlanDependencyDto,
  PlanWbsNodeDto,
} from "../api/planningFull";
import { buildPlanWorkspaceRows } from "./plan-workspace-rows";
import { formatPredecessor } from "./predecessor-syntax";
import { roundPlanProgress } from "./plan-progress";

export interface PlanningExportLabels {
  planInfoSheet: string;
  tasksSheet: string;
  project: string;
  projectCode: string;
  plan: string;
  planType: string;
  workingVersion: string;
  outline: string;
  taskId: string;
  taskName: string;
  taskType: string;
  schedulingMode: string;
  start: string;
  finish: string;
  duration: string;
  progress: string;
  predecessors: string;
  constraint: string;
  constraintDate: string;
  deadline: string;
  workPackage: string;
  task: string;
  milestone: string;
  summary: string;
}

export interface PlanningExportInput {
  project: { name: string; code: string };
  plan: { name: string; planType: string };
  workingVersionNumber: number;
  wbs: PlanWbsNodeDto[];
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  labels: PlanningExportLabels;
  localizeValue: (group: "planType" | "activityType" | "schedulingMode" | "constraintType" | "dependencyType", value: string) => string;
}

export interface PlanningExportTaskRow {
  outline: string;
  taskId: string;
  taskName: string;
  taskType: string;
  schedulingMode: string;
  start?: Date;
  finish?: Date;
  duration?: number;
  progress?: number;
  predecessors: string;
  constraint: string;
  constraintDate?: Date;
  deadline?: Date;
}

function excelDate(value?: string): Date | undefined {
  if (!value) return undefined;
  const date = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return undefined;
  const parsed = new Date(`${date}T00:00:00.000Z`);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed;
}

export function buildPlanningExportRows(input: PlanningExportInput): PlanningExportTaskRow[] {
  const rows = buildPlanWorkspaceRows(input.wbs, input.activities);
  const rowNumberByActivityId = new Map(
    rows.flatMap((row) => row.activity ? [[row.activity.id, row.rowNumber] as const] : []),
  );
  const activityById = new Map(input.activities.map((activity) => [activity.id, activity]));
  const dependenciesBySuccessor = new Map<string, PlanDependencyDto[]>();
  for (const dependency of input.dependencies) {
    const items = dependenciesBySuccessor.get(dependency.activityId) ?? [];
    items.push(dependency);
    dependenciesBySuccessor.set(dependency.activityId, items);
  }

  return rows.map((row) => {
    const activity = row.activity;
    const type = activity
      ? row.isSummary
        ? input.labels.summary
        : input.localizeValue("activityType", activity.type)
      : input.labels.workPackage;
    const predecessors = (dependenciesBySuccessor.get(activity?.id ?? "") ?? [])
      .flatMap((dependency) => {
        const predecessorRow = rowNumberByActivityId.get(dependency.predecessorActivityId);
        if (predecessorRow !== undefined) return [formatPredecessor(predecessorRow, dependency.type, dependency.lag)];
        const predecessor = activityById.get(dependency.predecessorActivityId);
        return predecessor?.code ? [`${predecessor.code} ${input.localizeValue("dependencyType", dependency.type)}`] : [];
      })
      .join(", ");

    return {
      outline: row.outlineNumber ?? "",
      taskId: activity?.code ?? row.wbs?.code ?? "",
      taskName: activity?.name ?? row.wbs?.name ?? "",
      taskType: type,
      schedulingMode: activity?.schedulingMode
        ? input.localizeValue("schedulingMode", activity.schedulingMode)
        : "",
      start: excelDate(activity?.startDate),
      finish: excelDate(activity?.endDate),
      duration: activity?.duration,
      progress: roundPlanProgress(activity?.progress) ?? undefined,
      predecessors,
      constraint: activity?.constraintType
        ? input.localizeValue("constraintType", activity.constraintType)
        : "",
      constraintDate: excelDate(activity?.constraintDate),
      deadline: excelDate(activity?.deadline),
    };
  });
}

export async function downloadPlanningWorkbook(input: PlanningExportInput): Promise<void> {
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  workbook.creator = "NEXUS PMO";
  workbook.subject = `${input.plan.name} plan export`;

  const info = workbook.addWorksheet(input.labels.planInfoSheet);
  info.addRows([
    [input.labels.project, input.project.name],
    [input.labels.projectCode, input.project.code],
    [input.labels.plan, input.plan.name],
    [input.labels.planType, input.localizeValue("planType", input.plan.planType)],
    [input.labels.workingVersion, input.workingVersionNumber],
  ]);
  info.getColumn(1).width = 24;
  info.getColumn(2).width = 42;
  info.eachRow((row) => {
    row.getCell(1).font = { bold: true };
    row.alignment = { vertical: "middle" };
  });

  const tasks = workbook.addWorksheet(input.labels.tasksSheet, {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  const headers = [
    input.labels.outline,
    input.labels.taskId,
    input.labels.taskName,
    input.labels.taskType,
    input.labels.schedulingMode,
    input.labels.start,
    input.labels.finish,
    input.labels.duration,
    input.labels.progress,
    input.labels.predecessors,
    input.labels.constraint,
    input.labels.constraintDate,
    input.labels.deadline,
  ];
  const header = tasks.addRow(headers);
  header.font = { bold: true, color: { argb: "FFFFFFFF" } };
  header.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF1F6E8C" } };
  header.alignment = { vertical: "middle", horizontal: "center", wrapText: true };
  header.height = 30;
  tasks.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: headers.length } };

  for (const task of buildPlanningExportRows(input)) {
    const row = tasks.addRow([
      task.outline,
      task.taskId,
      task.taskName,
      task.taskType,
      task.schedulingMode,
      task.start,
      task.finish,
      task.duration,
      task.progress,
      task.predecessors,
      task.constraint,
      task.constraintDate,
      task.deadline,
    ]);
    row.alignment = { vertical: "middle" };
    for (const column of [6, 7, 12, 13]) row.getCell(column).numFmt = "yyyy-mm-dd";
    row.getCell(9).numFmt = '0.##"%"';
  }

  const widths = [12, 18, 42, 18, 18, 14, 14, 12, 12, 24, 24, 18, 18];
  widths.forEach((width, index) => { tasks.getColumn(index + 1).width = width; });

  const filePart = input.plan.name.normalize("NFKC").replace(/[^\p{L}\p{N}._-]+/gu, "_").replace(/^_+|_+$/g, "") || "Plan";
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `NEXUS_${filePart}_Plan.xlsx`;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
