import * as React from "react";
import { api, setManagementRequestContext } from "./client";
import { endpoints } from "./endpoints";
import { MANAGEMENT_PERSPECTIVES, type ManagementPerspective, type ManagementRequestContext, type PerspectiveSnapshot } from "../lib/perspective";

interface ManagementContextValue {
  snapshot: PerspectiveSnapshot | null;
  context: ManagementRequestContext | null;
  loading: boolean;
  effectiveReadOnly: boolean;
  canMutate: boolean;
  setContext: (context: ManagementRequestContext | null) => void;
  switchPerspective: (perspective: ManagementPerspective, entityId?: string) => void;
  exitViewAs: () => void;
  refreshSnapshot: () => Promise<void>;
}

export function managementCacheScopeKey(userId: string | undefined, context: ManagementRequestContext | null): string {
  return [
    userId ?? "anonymous",
    context?.perspective ?? "native",
    context?.viewMode ?? "native",
    context?.portfolioId ?? "",
    context?.programId ?? "",
    context?.projectId ?? "",
  ].join(":");
}

const ManagementContext = React.createContext<ManagementContextValue | null>(null);
let bootstrapPromise: Promise<PerspectiveSnapshot> | null = null;

function bootstrap(): Promise<PerspectiveSnapshot> {
  if (bootstrapPromise) return bootstrapPromise;
  bootstrapPromise = api.get<PerspectiveSnapshot>(endpoints.auth.perspectives).finally(() => {
    bootstrapPromise = null;
  });
  return bootstrapPromise;
}

function toRequestContext(perspective: ManagementPerspective, isNative: boolean, entityId?: string): ManagementRequestContext {
  return {
    perspective,
    viewMode: isNative ? "Native" : "ReadOnlyViewAs",
    ...(perspective === MANAGEMENT_PERSPECTIVES.PortfolioManager ? { portfolioId: entityId } : {}),
    ...(perspective === MANAGEMENT_PERSPECTIVES.ProgramManager ? { programId: entityId } : {}),
    ...(perspective === MANAGEMENT_PERSPECTIVES.ProjectManager ? { projectId: entityId } : {}),
  };
}

export function ManagementProvider({ user, children }: { user: { id: string; permissions: string[] } | null; children: React.ReactNode }) {
  const [snapshot, setSnapshot] = React.useState<PerspectiveSnapshot | null>(null);
  const [context, setContextState] = React.useState<ManagementRequestContext | null>(null);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    let cancelled = false;
    if (!user) {
      setSnapshot(null);
      setContextState(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    bootstrap()
      .then((next) => {
        if (cancelled) return;
        setSnapshot(next);
        if (next.defaultPerspective) {
          const availability = next.availablePerspectives.find((a) => a.perspective === next.defaultPerspective);
          setContextState(toRequestContext(next.defaultPerspective, availability?.isNative ?? true, availability?.contexts[0]?.id));
        } else {
          // A user without a management assignment still has a valid tenant
          // context. Do not manufacture a PortfolioManager context: the API
          // correctly rejects perspectives the user does not own.
          setContextState(null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSnapshot(null);
          setContextState(null);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.id]);

  const setContext = React.useCallback((next: ManagementRequestContext | null) => setContextState(next), []);

  const switchPerspective = React.useCallback(
    (perspective: ManagementPerspective, entityId?: string) => {
      const availability = snapshot?.availablePerspectives.find((a) => a.perspective === perspective);
      if (!availability) return;
      const resolved = entityId ?? availability.contexts[0]?.id;
      if (availability.requiresEntityContext && !resolved) return;
      setContextState(toRequestContext(perspective, availability.isNative, resolved));
    },
    [snapshot]
  );

  const exitViewAs = React.useCallback(() => {
    const native = snapshot?.defaultPerspective;
    if (!native) return;
    const availability = snapshot?.availablePerspectives.find((a) => a.perspective === native);
    setContextState(toRequestContext(native, true, availability?.contexts[0]?.id));
  }, [snapshot]);

  const refreshSnapshot = React.useCallback(async () => {
    if (!user) return;
    try {
      const next = await bootstrap();
      setSnapshot(next);
      setContextState((current) => {
        if (current) {
          const available = next.availablePerspectives.find((item) => item.perspective === current.perspective);
          const selectedId = current.portfolioId ?? current.programId ?? current.projectId;
          const modeMatches = current.viewMode === "Native" ? available?.isNative : available && !available.isNative;
          if (available && modeMatches && selectedId && available.contexts.some((item) => item.id === selectedId)) return current;
        }
        if (!next.defaultPerspective) return null;
        const fallback = next.availablePerspectives.find((item) => item.perspective === next.defaultPerspective);
        return fallback ? toRequestContext(next.defaultPerspective, fallback.isNative, fallback.contexts[0]?.id) : null;
      });
    } catch {
      // Keep the last valid permission snapshot and management context on refresh failure.
    }
  }, [user]);

  React.useEffect(() => {
    setManagementRequestContext(context);
    return () => setManagementRequestContext(null);
  }, [context]);

  const value = React.useMemo<ManagementContextValue>(
    () => ({
      snapshot,
      context,
      loading,
      effectiveReadOnly: context?.viewMode === "ReadOnlyViewAs",
      canMutate: context?.viewMode !== "ReadOnlyViewAs",
      setContext,
      switchPerspective,
      exitViewAs,
      refreshSnapshot,
    }),
    [snapshot, context, loading, setContext, switchPerspective, exitViewAs, refreshSnapshot]
  );

  return <ManagementContext.Provider value={value}>{children}</ManagementContext.Provider>;
}

export function useManagement() {
  const value = React.useContext(ManagementContext);
  if (!value) throw new Error("useManagement must be used within ManagementProvider");
  return value;
}

// Back-compat alias used by Next.js code paths
export const useManagementPerspective = useManagement;
