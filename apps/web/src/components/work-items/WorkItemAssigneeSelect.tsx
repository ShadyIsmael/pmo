"use client";

import { useMemo } from "react";
import { SearchableResourceSelect, type ResourceSearchOption } from "@/components/projects/SearchableResourceSelect";
import type { WorkItemAssigneeDto } from "@/lib/api/work-items";

export function WorkItemAssigneeSelect({
  value,
  candidates,
  onValueChange,
  placeholder,
  searchPlaceholder,
  emptyText,
  clearLabel,
  loading = false,
  disabled = false,
}: {
  value?: string;
  candidates: readonly WorkItemAssigneeDto[];
  onValueChange: (_value: string) => void;
  placeholder: string;
  searchPlaceholder: string;
  emptyText: string;
  clearLabel: string;
  loading?: boolean;
  disabled?: boolean;
}) {
  const options = useMemo<ResourceSearchOption[]>(() => candidates.map((candidate) => ({
      value: candidate.assigneeId,
      label: candidate.name,
      email: candidate.email,
      jobTitle: candidate.jobTitle,
    })), [candidates]);

  return <SearchableResourceSelect
    value={value}
    onValueChange={onValueChange}
    placeholder={placeholder}
    searchPlaceholder={searchPlaceholder}
    emptyText={emptyText}
    clearLabel={clearLabel}
    clearable
    staticOptions={options}
    loading={loading}
    disabled={disabled}
  />;
}
