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
  /** True when the row aggregates child rows (WBS parent or backend summary activity). */
  isSummary: boolean;
  /** Microsoft Project-style hierarchical outline number (e.g. "1.2.1"); display-only. */
  outlineNumber?: string;
}

function compareOrdinal(left: string, right: string) {
  return left < right ? -1 : left > right ? 1 : 0;
}

export function comparePlanOrder(left: { sortOrder?: number; code: string; id: string }, right: { sortOrder?: number; code: string; id: string }) {
  return (left.sortOrder ?? 0) - (right.sortOrder ?? 0) || compareOrdinal(left.code, right.code) || compareOrdinal(left.id, right.id);
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
  for (const linked of activitiesByWbs.values()) linked.sort(comparePlanOrder);
  const sortedActivities = [...activities].sort(comparePlanOrder);
  const allNodes: PlanWbsNodeDto[] = [];
  const nodesById = new Map<string, PlanWbsNodeDto>();
  const collectNodes = (nodes: PlanWbsNodeDto[]) => {
    for (const node of nodes) {
      if (nodesById.has(node.id)) continue;
      nodesById.set(node.id, node);
      allNodes.push(node);
      collectNodes(node.children ?? []);
    }
  };
  collectNodes(wbs);

  // Rebuild sibling groups from the persisted parent IDs instead of relying
  // solely on the nested response. That keeps a row visible if a legacy or
  // partially linked activity/WBS association is absent from `children`.
  const childrenByParent = new Map<string | null, PlanWbsNodeDto[]>();
  for (const node of allNodes) {
    const parentId = node.parentWbsId && nodesById.has(node.parentWbsId) ? node.parentWbsId : null;
    const siblings = childrenByParent.get(parentId);
    if (siblings) siblings.push(node);
    else childrenByParent.set(parentId, [node]);
  }

  const visitedNodes = new Set<string>();
  const visitNode = (node: PlanWbsNodeDto, depth: number, outlineNumber: string) => {
    if (visitedNodes.has(node.id)) return;
    visitedNodes.add(node.id);
    const linked = activitiesByWbs.get(node.id) ?? [];
    const explicitPrimary = node.activityId ? activitiesById.get(node.activityId) : undefined;
    const primary = explicitPrimary && !placedActivities.has(explicitPrimary.id)
      ? explicitPrimary
      : linked.find((item) => !placedActivities.has(item.id));
    if (primary) placedActivities.add(primary.id);
    const additionalActivities = linked.filter((item) => item.id !== primary?.id && !placedActivities.has(item.id));
    const childNodes = childrenByParent.get(node.id) ?? [];
    const hasChildren = childNodes.length > 0 || additionalActivities.length > 0;
    rows.push({
      id: primary ? `activity:${primary.id}` : `wbs:${node.id}`,
      depth,
      activity: primary,
      wbs: node,
      textFormat: primary?.textFormat ?? node.textFormat,
      hasChildren,
      isSummary: hasChildren || Boolean(primary?.isSummaryTask),
      outlineNumber,
    });

    // Activities linked to one WBS node and its child WBS nodes share one
    // sibling sequence. Sorting them separately made a Task/Milestone jump
    // above or below its WBS siblings after a reload.
    const childItems = [
      ...childNodes.map((child) => ({ kind: "wbs" as const, item: child })),
      ...additionalActivities.map((activity) => ({ kind: "activity" as const, item: activity })),
    ].sort((left, right) => comparePlanOrder(left.item, right.item));
    childItems.forEach((child, index) => {
      const childOutline = `${outlineNumber}.${index + 1}`;
      if (child.kind === "wbs") {
        visitNode(child.item, depth + 1, childOutline);
        return;
      }
      placedActivities.add(child.item.id);
      rows.push({
        id: `activity:${child.item.id}`,
        depth: depth + 1,
        activity: child.item,
        textFormat: child.item.textFormat,
        hasChildren: Boolean(child.item.isSummaryTask),
        isSummary: Boolean(child.item.isSummaryTask),
        outlineNumber: childOutline,
      });
    });
  };

  const activitiesRepresentedByWbs = new Set(
    allNodes.flatMap((node) => [
      ...(node.activityId ? [node.activityId] : []),
      ...(activitiesByWbs.get(node.id) ?? []).map((activity) => activity.id),
    ]),
  );
  const rootActivities = sortedActivities.filter((item) => !activitiesRepresentedByWbs.has(item.id));
  const rootItems = [
    ...(childrenByParent.get(null) ?? []).map((node) => ({ kind: "wbs" as const, item: node })),
    ...rootActivities.map((activity) => ({ kind: "activity" as const, item: activity })),
  ].sort((left, right) => comparePlanOrder(left.item, right.item));
  rootItems.forEach((root, index) => {
    const outlineNumber = String(index + 1);
    if (root.kind === "wbs") {
      visitNode(root.item, 0, outlineNumber);
      return;
    }
    placedActivities.add(root.item.id);
    rows.push({
      id: `activity:${root.item.id}`,
      depth: 0,
      activity: root.item,
      textFormat: root.item.textFormat,
      hasChildren: Boolean(root.item.isSummaryTask),
      isSummary: Boolean(root.item.isSummaryTask),
      outlineNumber,
    });
  });

  // Cycles or malformed parent chains have no root. Keep those records visible
  // as top-level rows while the API validation remains authoritative for edits.
  const unvisitedNodes = allNodes.filter((node) => !visitedNodes.has(node.id)).sort(comparePlanOrder);
  for (const node of unvisitedNodes) visitNode(node, 0, String(rows.length + 1));

  // Defensive fallback for an activity link that pointed to a node in a
  // malformed cyclic group. It remains visible, even when it cannot be placed
  // beneath that node safely.
  for (const activity of sortedActivities.filter((item) => !placedActivities.has(item.id))) {
    rows.push({
      id: `activity:${activity.id}`,
      depth: 0,
      activity,
      textFormat: activity.textFormat,
      hasChildren: Boolean(activity.isSummaryTask),
      isSummary: Boolean(activity.isSummaryTask),
    });
  }
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

export function collapsePlanWorkspaceToOutlineLevel(rows: PlanWorkspaceRow[], outlineLevel: number) {
  const level = Math.max(1, Math.floor(outlineLevel));
  return new Set(rows.flatMap((row) => row.wbs && row.hasChildren && row.depth + 1 >= level ? [row.wbs.id] : []));
}

export function getPlanWorkspaceOutlineLevel(rows: PlanWorkspaceRow[], collapsedWbsIds: ReadonlySet<string>) {
  const maxLevel = Math.max(1, ...rows.map((row) => row.depth + 1));
  for (let level = 1; level <= maxLevel; level += 1) {
    const expected = collapsePlanWorkspaceToOutlineLevel(rows, level);
    if (expected.size === collapsedWbsIds.size && [...expected].every((id) => collapsedWbsIds.has(id))) return String(level);
  }
  return "all";
}

export function findWbsNode(nodes: PlanWbsNodeDto[], id: string): PlanWbsNodeDto | undefined {
  for (const node of nodes) {
    if (node.id === id) return node;
    const match = findWbsNode(node.children ?? [], id);
    if (match) return match;
  }
  return undefined;
}
