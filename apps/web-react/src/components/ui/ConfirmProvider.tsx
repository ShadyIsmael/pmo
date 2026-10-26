import * as React from "react";
import { ConfirmDialog } from "./ConfirmDialog";

export interface ConfirmOptions {
  description: string;
  title?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

type Translate = (key: string) => string;
type PendingConfirmation = ConfirmOptions & { resolve: (accepted: boolean) => void };

const ConfirmContext = React.createContext<((options: ConfirmOptions) => Promise<boolean>) | null>(null);

export function useConfirm(): (options: ConfirmOptions) => Promise<boolean> {
  const confirm = React.useContext(ConfirmContext);
  if (!confirm) throw new Error("useConfirm must be used within I18nProvider");
  return confirm;
}

export function ConfirmProvider({ children, translate }: { children: React.ReactNode; translate: Translate }) {
  const [activeRequest, setActiveRequest] = React.useState<PendingConfirmation | null>(null);
  const activeRequestRef = React.useRef<PendingConfirmation | null>(null);
  const queuedRequestsRef = React.useRef<PendingConfirmation[]>([]);
  const acceptedRef = React.useRef(false);

  const confirm = React.useCallback((options: ConfirmOptions) => new Promise<boolean>((resolve) => {
    const request: PendingConfirmation = { ...options, resolve };
    if (activeRequestRef.current) {
      queuedRequestsRef.current.push(request);
      return;
    }
    activeRequestRef.current = request;
    setActiveRequest(request);
  }), []);

  const finishRequest = React.useCallback((accepted: boolean) => {
    const completed = activeRequestRef.current;
    if (!completed) return;

    const next = queuedRequestsRef.current.shift() ?? null;
    activeRequestRef.current = next;
    acceptedRef.current = false;
    setActiveRequest(next);
    completed.resolve(accepted);
  }, []);

  React.useEffect(() => () => {
    const pending = [activeRequestRef.current, ...queuedRequestsRef.current].filter((item): item is PendingConfirmation => item !== null);
    activeRequestRef.current = null;
    queuedRequestsRef.current = [];
    pending.forEach((request) => request.resolve(false));
  }, []);

  const value = React.useMemo(() => confirm, [confirm]);
  return (
    <ConfirmContext.Provider value={value}>
      {children}
      <ConfirmDialog
        open={activeRequest !== null}
        onOpenChange={(open) => {
          if (open) return;
          finishRequest(acceptedRef.current);
        }}
        title={activeRequest?.title ?? translate("common.confirmationTitle")}
        description={activeRequest?.description}
        confirmLabel={activeRequest?.confirmLabel ?? translate("common.confirm")}
        cancelLabel={activeRequest?.cancelLabel ?? translate("common.cancel")}
        destructive={activeRequest?.destructive}
        onConfirm={() => { acceptedRef.current = true; }}
      />
    </ConfirmContext.Provider>
  );
}
