import * as React from "react";
import { api, setManagementRequestContext, type CurrentUser, type ManagementRequestContext } from "./client";

export interface ManagementEntityReference { id: string; name: string; code?: string; }
export interface PerspectiveAvailability { perspective: string; isNative: boolean; requiresEntityContext: boolean; contexts: ManagementEntityReference[]; }
export interface PerspectiveSnapshot { nativePerspectives: string[]; availablePerspectives: PerspectiveAvailability[]; defaultPerspective?: string; canViewAs: boolean; selectedContext?: { selectedPerspective: string; viewMode: "Native" | "ReadOnlyViewAs"; selectedPortfolioId?: string; selectedProgramId?: string; selectedProjectId?: string; effectiveReadOnly: boolean; }; }

function defaultContext(snapshot: PerspectiveSnapshot): ManagementRequestContext | null {
  if (!snapshot.defaultPerspective) return null;
  const availability = snapshot.availablePerspectives.find((item) => item.perspective === snapshot.defaultPerspective);
  const entityId = availability?.contexts[0]?.id;
  if (snapshot.selectedContext) return { perspective: snapshot.selectedContext.selectedPerspective, viewMode: snapshot.selectedContext.viewMode, portfolioId: snapshot.selectedContext.selectedPortfolioId, programId: snapshot.selectedContext.selectedProgramId, projectId: snapshot.selectedContext.selectedProjectId };
  return { perspective: snapshot.defaultPerspective, viewMode: availability?.isNative === false ? "ReadOnlyViewAs" : "Native", ...(snapshot.defaultPerspective === "PortfolioManager" && entityId ? { portfolioId: entityId } : {}), ...(snapshot.defaultPerspective === "ProgramManager" && entityId ? { programId: entityId } : {}), ...(snapshot.defaultPerspective === "ProjectManager" && entityId ? { projectId: entityId } : {}) };
}

interface ManagementContextValue { snapshot: PerspectiveSnapshot | null; current: ManagementRequestContext | null; loading: boolean; effectiveReadOnly: boolean; }
const ManagementContext = React.createContext<ManagementContextValue | null>(null);

export function ManagementProvider({ children, user }: { children: React.ReactNode; user: CurrentUser | null }) {
  const [snapshot, setSnapshot] = React.useState<PerspectiveSnapshot | null>(null);
  const [current, setCurrent] = React.useState<ManagementRequestContext | null>(null);
  const [loading, setLoading] = React.useState(Boolean(user));
  React.useEffect(() => {
    let cancelled = false;
    if (!user) { setSnapshot(null); setCurrent(null); setLoading(false); setManagementRequestContext(null); return () => { cancelled = true; }; }
    setLoading(true);
    api.get<PerspectiveSnapshot>("/api/me/perspectives").then((next) => { if (cancelled) return; const nextContext = defaultContext(next); setSnapshot(next); setCurrent(nextContext); setManagementRequestContext(nextContext); }).catch(() => { if (!cancelled) { setSnapshot(null); setCurrent(null); setManagementRequestContext(null); } }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [user]);
  const value = React.useMemo(() => ({ snapshot, current, loading, effectiveReadOnly: current?.viewMode === "ReadOnlyViewAs" }), [snapshot, current, loading]);
  return <ManagementContext.Provider value={value}>{children}</ManagementContext.Provider>;
}

export function useManagement(): ManagementContextValue { const value = React.useContext(ManagementContext); if (!value) throw new Error("useManagement must be used within ManagementProvider"); return value; }
