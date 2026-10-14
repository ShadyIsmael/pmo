export function isAllVisibleWorkItemsSelected(selectedIds: ReadonlySet<string>, visibleIds: readonly string[]): boolean {
  return visibleIds.length > 0 && visibleIds.every((id) => selectedIds.has(id));
}

export function toggleWorkItemSelection(selectedIds: ReadonlySet<string>, itemId: string): Set<string> {
  const next = new Set(selectedIds);
  if (next.has(itemId)) next.delete(itemId);
  else next.add(itemId);
  return next;
}

export function toggleAllVisibleWorkItemSelection(selectedIds: ReadonlySet<string>, visibleIds: readonly string[]): Set<string> {
  const next = new Set(selectedIds);
  if (isAllVisibleWorkItemsSelected(selectedIds, visibleIds)) visibleIds.forEach((id) => next.delete(id));
  else visibleIds.forEach((id) => next.add(id));
  return next;
}
