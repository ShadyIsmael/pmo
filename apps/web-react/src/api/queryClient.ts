import { QueryClient } from "@tanstack/react-query";
import { ApiError, SessionChangedError } from "./client";

function shouldRetry(failureCount: number, error: unknown): boolean {
  // A session/context fence is terminal. Retrying the same query or mutation
  // would replay work with whichever account became current after the switch.
  const isAbortError = typeof error === "object" && error !== null && "name" in error && (error as { name?: unknown }).name === "AbortError";
  if (error instanceof SessionChangedError || isAbortError) return false;
  if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403 || (error.status >= 400 && error.status < 500)) return false;
  }
  return failureCount < 1;
}

/** A private query/mutation cache owned by one authenticated client session. */
export function createSessionQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: shouldRetry, refetchOnWindowFocus: false },
      // Mutations may have committed before a 5xx/connection failure reached
      // the client. Replaying them can duplicate non-idempotent actions such
      // as capturing a new immutable plan Baseline.
      mutations: { retry: false },
    },
  });
}
