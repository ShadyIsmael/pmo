import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, getClientSessionId, setManagementRequestContext } from "./client";
import { endpoints } from "./endpoints";
import { MANAGEMENT_PERSPECTIVES, type ManagementPerspective, type ManagementRequestContext, type PerspectiveSnapshot } from "../lib/perspective";

interface ManagementContextValue {
  snapshot: PerspectiveSnapshot | null;
  context: ManagementRequestContext | null;
  loading: boolean;
  initializationError: boolean;
  effectiveReadOnly: boolean;
  canMutate: boolean;
  setContext: (context: ManagementRequestContext | null) => void;
  switchPerspective: (perspective: ManagementPerspective, entityId?: string) => void;
  exitViewAs: () => void;
  refreshSnapshot: () => Promise<void>;
  retryInitialization: () => Promise<void>;
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

function toRequestContext(perspective: ManagementPerspective, isNative: boolean, entityId?: string): ManagementRequestContext {
  return {
    perspective,
    viewMode: isNative ? "Native" : "ReadOnlyViewAs",
    ...(perspective === MANAGEMENT_PERSPECTIVES.PortfolioManager ? { portfolioId: entityId } : {}),
    ...(perspective === MANAGEMENT_PERSPECTIVES.ProgramManager ? { programId: entityId } : {}),
    ...(perspective === MANAGEMENT_PERSPECTIVES.ProjectManager ? { projectId: entityId } : {}),
  };
}

function defaultEntityContextId(perspective: ManagementPerspective, isNative: boolean, availability?: PerspectiveSnapshot["availablePerspectives"][number]) {
  // Native Project Managers can manage all projects assigned to them. Selecting
  // the first project by default narrows collection endpoints such as /projects
  // to one project and hides later handovers from the project registry.
  if (perspective === MANAGEMENT_PERSPECTIVES.ProjectManager && isNative) return undefined;
  return availability?.contexts[0]?.id;
}

export function ManagementProvider({ user, sessionId = getClientSessionId(), children }: { user: { id: string; permissions: string[] } | null; sessionId?: string | null; children: React.ReactNode }) {
  const queryClient = useQueryClient();
  const [snapshot, setSnapshot] = React.useState<PerspectiveSnapshot | null>(null);
  const [context, setContextState] = React.useState<ManagementRequestContext | null>(null);
  const contextRef = React.useRef<ManagementRequestContext | null>(null);
  const snapshotRequest = React.useRef<Promise<PerspectiveSnapshot> | null>(null);
  const [loadingState, setLoadingState] = React.useState(true);
  const [initializedUserId, setInitializedUserId] = React.useState<string | null>(null);
  const [errorUserId, setErrorUserId] = React.useState<string | null>(null);
  const loading = Boolean(user) && (loadingState || initializedUserId !== user?.id);
  const initializationError = Boolean(user && errorUserId === user.id && !loading);

  const bootstrap = React.useCallback(() => {
    if (!snapshotRequest.current) {
      snapshotRequest.current = api.get<PerspectiveSnapshot>(endpoints.auth.perspectives).finally(() => {
        snapshotRequest.current = null;
      });
    }
    return snapshotRequest.current;
  }, []);

  const applyContext = React.useCallback((next: ManagementRequestContext | null) => {
    if (sessionId !== getClientSessionId()) return;
    const changed = JSON.stringify(next) !== JSON.stringify(contextRef.current);
    if (!setManagementRequestContext(next, sessionId)) return;
    contextRef.current = next;
    setContextState(next);
    if (changed) {
      void queryClient.cancelQueries();
      queryClient.clear();
    }
  }, [queryClient, sessionId]);

  React.useEffect(() => {
    let cancelled = false;
    if (!user) {
      setSnapshot(null);
      contextRef.current = null;
      setContextState(null);
      setInitializedUserId(null);
      setErrorUserId(null);
      setLoadingState(false);
      setManagementRequestContext(null, sessionId);
      return;
    }
    setLoadingState(true);
    setErrorUserId(null);
    bootstrap()
      .then((next) => {
        if (cancelled) return;
        setSnapshot(next);
        if (next.defaultPerspective) {
          const availability = next.availablePerspectives.find((a) => a.perspective === next.defaultPerspective);
          const isNative = availability?.isNative ?? true;
          applyContext(toRequestContext(next.defaultPerspective, isNative, defaultEntityContextId(next.defaultPerspective, isNative, availability)));
        } else {
          // A user without a management assignment still has a valid tenant
          // context. Do not manufacture a PortfolioManager context: the API
          // correctly rejects perspectives the user does not own.
          applyContext(null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSnapshot(null);
          applyContext(null);
          setErrorUserId(user.id);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setInitializedUserId(user.id);
          setLoadingState(false);
        }
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.id, sessionId, bootstrap, applyContext]);

  const setContext = React.useCallback((next: ManagementRequestContext | null) => applyContext(next), [applyContext]);

  const switchPerspective = React.useCallback(
    (perspective: ManagementPerspective, entityId?: string) => {
      const availability = snapshot?.availablePerspectives.find((a) => a.perspective === perspective);
      if (!availability) return;
      const resolved = entityId ?? defaultEntityContextId(perspective, availability.isNative, availability);
      const unscopedNativeProjectManager = perspective === MANAGEMENT_PERSPECTIVES.ProjectManager && availability.isNative;
      if (availability.requiresEntityContext && !resolved && !unscopedNativeProjectManager) return;
      applyContext(toRequestContext(perspective, availability.isNative, resolved));
    },
    [snapshot, applyContext]
  );

  const exitViewAs = React.useCallback(() => {
    const native = snapshot?.defaultPerspective;
    if (!native) return;
    const availability = snapshot?.availablePerspectives.find((a) => a.perspective === native);
    applyContext(toRequestContext(native, true, defaultEntityContextId(native, true, availability)));
  }, [snapshot, applyContext]);

  const refreshSnapshot = React.useCallback(async () => {
    if (!user) return;
    try {
      const next = await bootstrap();
      setSnapshot(next);
      const current = contextRef.current;
      let nextContext: ManagementRequestContext | null;
      if (current) {
        const available = next.availablePerspectives.find((item) => item.perspective === current.perspective);
        const selectedId = current.portfolioId ?? current.programId ?? current.projectId;
        const modeMatches = current.viewMode === "Native" ? available?.isNative : available && !available.isNative;
        const unscopedNativeProjectManager = current.perspective === MANAGEMENT_PERSPECTIVES.ProjectManager
          && current.viewMode === "Native" && !selectedId && available?.isNative;
        if (available && modeMatches && (unscopedNativeProjectManager || (selectedId && available.contexts.some((item) => item.id === selectedId)))) {
          nextContext = current;
          applyContext(nextContext);
          return;
        }
      }
      if (!next.defaultPerspective) {
        applyContext(null);
        return;
      }
      const fallback = next.availablePerspectives.find((item) => item.perspective === next.defaultPerspective);
      nextContext = fallback ? toRequestContext(next.defaultPerspective, fallback.isNative, defaultEntityContextId(next.defaultPerspective, fallback.isNative, fallback)) : null;
      applyContext(nextContext);
    } catch {
      // Keep the last valid permission snapshot and management context on refresh failure.
    }
  }, [user, bootstrap, applyContext]);

  const retryInitialization = React.useCallback(async () => {
    if (!user) return;
    setLoadingState(true);
    setErrorUserId(null);
    try {
      const next = await bootstrap();
      setSnapshot(next);
      const availability = next.availablePerspectives.find((item) => item.perspective === next.defaultPerspective);
      applyContext(next.defaultPerspective && availability
        ? toRequestContext(next.defaultPerspective, availability.isNative, defaultEntityContextId(next.defaultPerspective, availability.isNative, availability))
        : null);
      setInitializedUserId(user.id);
    } catch {
      setErrorUserId(user.id);
    } finally {
      setLoadingState(false);
    }
  }, [user, bootstrap, applyContext]);

  const value = React.useMemo<ManagementContextValue>(
    () => ({
      snapshot,
      context,
      loading,
      initializationError,
      effectiveReadOnly: context?.viewMode === "ReadOnlyViewAs",
      canMutate: context?.viewMode !== "ReadOnlyViewAs",
      setContext,
      switchPerspective,
      exitViewAs,
      refreshSnapshot,
      retryInitialization,
    }),
    [snapshot, context, loading, initializationError, setContext, switchPerspective, exitViewAs, refreshSnapshot, retryInitialization]
  );

  return <ManagementContext.Provider value={value}>{children}</ManagementContext.Provider>;
}

export function useManagement() {
  const value = React.useContext(ManagementContext);
  if (!value) throw new Error("useManagement must be used within ManagementProvider");
  return value;
}

