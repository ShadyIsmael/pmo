"use client";

import * as React from "react";
import { api, clearPerspectiveRequestContext, setPerspectiveRequestContext, type CurrentUser } from "@/lib/api/client";
import { useAuth } from "@/lib/auth";
import {
  MANAGEMENT_PERSPECTIVES,
  type ManagementPerspective,
  type ManagementRequestContext,
  type PerspectiveAvailability,
  type PerspectiveSnapshot,
} from "@/lib/perspective";

interface PerspectiveContextValue {
  snapshot: PerspectiveSnapshot | null;
  current: ManagementRequestContext | null;
  availablePerspectives: PerspectiveAvailability[];
  effectiveReadOnly: boolean;
  canMutate: boolean;
  loading: boolean;
  switchPerspective: (_perspective: ManagementPerspective, _entityId?: string) => void;
  exitViewAs: () => void;
}

const PerspectiveContext = React.createContext<PerspectiveContextValue | null>(null);

export function ManagementPerspectiveProvider({ children, user }: { children: React.ReactNode; user: CurrentUser | null }) {
  const [snapshot, setSnapshot] = React.useState<PerspectiveSnapshot | null>(null);
  const [current, setCurrent] = React.useState<ManagementRequestContext | null>(null);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    let cancelled = false;
    if (!user) {
      setSnapshot(null);
      setCurrent(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    api.getPerspectives()
      .then((next) => {
        if (cancelled) return;
        setSnapshot(next);
        if (next.defaultPerspective) {
          const availability = next.availablePerspectives.find((item) => item.perspective === next.defaultPerspective);
          const entityId = availability?.contexts[0]?.id;
          const context = toRequestContext(next.defaultPerspective, availability?.isNative ?? true, entityId);
          setCurrent(context);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSnapshot(null);
          setCurrent(null);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [user]);

  React.useEffect(() => {
    if (current) setPerspectiveRequestContext(current);
    else clearPerspectiveRequestContext();
  }, [current]);

  const switchPerspective = React.useCallback((perspective: ManagementPerspective, entityId?: string) => {
    const availability = snapshot?.availablePerspectives.find((item) => item.perspective === perspective);
    if (!availability) return;
    const resolvedEntityId = entityId ?? availability.contexts[0]?.id;
    if (availability.requiresEntityContext && !resolvedEntityId) return;
    setCurrent(toRequestContext(perspective, availability.isNative, resolvedEntityId));
  }, [snapshot]);

  const exitViewAs = React.useCallback(() => {
    const native = snapshot?.defaultPerspective;
    if (!native) return;
    const availability = snapshot.availablePerspectives.find((item) => item.perspective === native);
    setCurrent(toRequestContext(native, true, availability?.contexts[0]?.id));
  }, [snapshot]);

  const value = React.useMemo<PerspectiveContextValue>(() => ({
    snapshot,
    current,
    availablePerspectives: snapshot?.availablePerspectives ?? [],
    effectiveReadOnly: current?.viewMode === "ReadOnlyViewAs",
    canMutate: current?.viewMode !== "ReadOnlyViewAs",
    loading,
    switchPerspective,
    exitViewAs,
  }), [snapshot, current, loading, switchPerspective, exitViewAs]);

  return <PerspectiveContext.Provider value={value}>{children}</PerspectiveContext.Provider>;
}

export function ManagementPerspectiveBridge({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  return <ManagementPerspectiveProvider user={user}>{children}</ManagementPerspectiveProvider>;
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

export function useManagementPerspective(): PerspectiveContextValue {
  const value = React.useContext(PerspectiveContext);
  if (!value) throw new Error("useManagementPerspective must be used within ManagementPerspectiveProvider");
  return value;
}
