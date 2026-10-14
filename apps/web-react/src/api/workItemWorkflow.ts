import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "./client";
import { workItemsApiFull, workItemKeys, type WorkflowTransitionOption, type WorkflowTransitionResult } from "./workItemsFull";

export type WorkflowMoveFailure = { kind: "unavailable"; message?: string } | { kind: "multiple"; options: WorkflowTransitionOption[] };

export class WorkflowMoveError extends Error {
  constructor(public readonly failure: WorkflowMoveFailure) {
    super(failure.kind === "multiple" ? "Multiple transitions are available for this target state. Select a transition." : failure.message ?? "Transition unavailable");
    this.name = "WorkflowMoveError";
  }
}

export function workflowMoveErrorMessage(cause: unknown, fallback: string): string {
  if (cause instanceof ApiError) return cause.message;
  if (cause instanceof WorkflowMoveError && cause.failure.kind === "unavailable") return cause.failure.message || fallback;
  return fallback;
}

export interface WorkflowMoveVariables { itemId: string; targetStateCode: string; transitionCode?: string }

export function useWorkItemWorkflowTransition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ itemId, targetStateCode, transitionCode }: WorkflowMoveVariables): Promise<WorkflowTransitionResult> => {
      const available = await workItemsApiFull.workflowTransitions(itemId);
      const candidates = available.transitions.filter((option) => option.targetStateCode === targetStateCode && option.canExecute);
      if (candidates.length === 0) throw new WorkflowMoveError({ kind: "unavailable", message: available.transitions.find((option) => option.targetStateCode === targetStateCode)?.denialReason });
      const selected = transitionCode ? candidates.find((option) => option.code === transitionCode) : candidates.length === 1 ? candidates[0] : undefined;
      if (!selected) throw new WorkflowMoveError({ kind: "multiple", options: candidates });
      return workItemsApiFull.executeWorkflowTransition(itemId, selected.code, { expectedStateCode: available.currentStateCode, expectedRowVersion: available.currentRowVersion });
    },
    onSuccess: async (_result, variables) => {
      await queryClient.invalidateQueries({ queryKey: workItemKeys.all });
      await queryClient.invalidateQueries({ queryKey: ["work-items", "workflow", variables.itemId] });
    },
  });
}
