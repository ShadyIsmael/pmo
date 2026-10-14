import type { PlanActivityDto, PlanWbsNodeDto } from "../api/planningFull";

export interface PlanTextFormatDto {
  isBold?: boolean;
  isItalic?: boolean;
  isUnderline?: boolean;
  textColorToken?: string;
  highlightColorToken?: string;
  fontSizeToken?: string;
}

export interface PlanWorkspaceRow {
  id: string;
  rowNumber: number;
  depth: number;
  activity?: PlanActivityDto;
  wbs?: PlanWbsNodeDto;
  textFormat?: PlanTextFormatDto | null;
  hasChildren: boolean;
}

function compareOrder(left: { sortOrder?: number; code: string }, right: { sortOrder?: number; code: string }) {
  return (left.sortOrder ?? 0) - (right.sortOrder ?? 0) || left.code.localeCompare(right.code);
}

export function buildPlanWorkspaceRows(wbs: PlanWbsNodeDto[], activities: PlanActivityDto[]): PlanWorkspaceRow[] {
  const rows: Omit<PlanWorkspaceRow, "rowNumber">[] = [];
  const placedActivities = new Set<string>();
  const activitiesById = new Map(activities.map((activity) => [activity.id, activity]));
  const activitiesByWbs = new Map<string, PlanActivityDto[]>();
  for (const activity of activities) {
    if (!activity.wbsId) continue;
    const linked = activitiesByWbs.get(activity.wbsId);
    if (linked) linked.push(activity);
    else activitiesByWbs.set(activity.wbsId, [activity]);
  }
  for (const linked of activitiesByWbs.values()) linked.sort(compareOrder);
  const sortedActivities = [...activities].sort(compareOrder);
  const visit = (nodes: PlanWbsNodeDto[], depth: number) => {
    for (const node of nodes.slice().sort(compareOrder)) {
      const linked = activitiesByWbs.get(node.id) ?? [];
      const primary = (node.activityId ? activitiesById.get(node.activityId) : undefined) ?? linked[0];
      if (primary) placedActivities.add(primary.id);
      rows.push({ id: primary ? `activity:${primary.id}` : `wbs:${node.id}`, depth, activity: primary, wbs: node, textFormat: primary?.textFormat ?? node.textFormat, hasChildren: (node.children?.length ?? 0) > 0 || linked.length > 1 });
      for (const activity of linked.filter((item) => item.id !== primary?.id)) {
        placedActivities.add(activity.id);
        rows.push({ id: `activity:${activity.id}`, depth: depth + 1, activity, textFormat: activity.textFormat, hasChildren: false });
      }
      visit(node.children ?? [], depth + 1);
    }
  };
  visit(wbs, 0);
  for (const activity of sortedActivities.filter((item) => !placedActivities.has(item.id))) rows.push({ id: `activity:${activity.id}`, depth: 0, activity, textFormat: activity.textFormat, hasChildren: false });
  return rows.map((row, index) => ({ ...row, rowNumber: index + 1 }));
}

export function visiblePlanWorkspaceRows(rows: PlanWorkspaceRow[], collapsedWbsIds: ReadonlySet<string>) {
  const visible: PlanWorkspaceRow[] = [];
  const hiddenDepths: number[] = [];
  for (const row of rows) {
    while (hiddenDepths.length > 0 && row.depth <= hiddenDepths[hiddenDepths.length - 1]) hiddenDepths.pop();
    if (hiddenDepths.length === 0) visible.push(row);
    if (row.wbs && collapsedWbsIds.has(row.wbs.id)) hiddenDepths.push(row.depth);
  }
  return visible;
}

export function findWbsNode(nodes: PlanWbsNodeDto[], id: string): PlanWbsNodeDto | undefined {
  for (const node of nodes) {
    if (node.id === id) return node;
    const match = findWbsNode(node.children ?? [], id);
    if (match) return match;
  }
  return undefined;
}
