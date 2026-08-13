"use client";

import * as React from "react";
import dynamic from "next/dynamic";

// The sheet bundles several lookup queries (users, portfolios, programs,
// departments, currencies). Lazy-load it and mount it only while open so those
// requests never fire for a closed sheet on any dashboard route.
const LazyCreateProjectSheet = dynamic(
  () =>
    import("@/components/projects/CreateProjectSheet").then(
      (mod) => mod.CreateProjectSheet
    ),
  { ssr: false }
);

interface CreateProjectSheetContextValue {
  openCreateProject: () => void;
  closeCreateProject: () => void;
}

const CreateProjectSheetContext = React.createContext<CreateProjectSheetContextValue | null>(null);

export function useCreateProjectSheet(): CreateProjectSheetContextValue {
  const ctx = React.useContext(CreateProjectSheetContext);
  if (!ctx) {
    throw new Error("useCreateProjectSheet must be used within CreateProjectSheetProvider");
  }
  return ctx;
}

export function CreateProjectSheetProvider({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = React.useState(false);

  const value = React.useMemo<CreateProjectSheetContextValue>(
    () => ({
      openCreateProject: () => setOpen(true),
      closeCreateProject: () => setOpen(false),
    }),
    []
  );

  return (
    <CreateProjectSheetContext.Provider value={value}>
      {children}
      {open ? <LazyCreateProjectSheet open={open} onOpenChange={setOpen} /> : null}
    </CreateProjectSheetContext.Provider>
  );
}
