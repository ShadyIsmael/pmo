import * as React from "react";

type ToastVariant = "default" | "destructive" | "success";

interface ToastItem {
  id: string;
  title?: string;
  description?: string;
  variant?: ToastVariant;
}

interface ToastState {
  toasts: ToastItem[];
}

let count = 0;
function genId() {
  count = (count + 1) % Number.MAX_SAFE_INTEGER;
  return count.toString();
}

const listeners: Array<(state: ToastState) => void> = [];
let memoryState: ToastState = { toasts: [] };

function dispatch(toasts: ToastItem[]) {
  memoryState = { toasts };
  listeners.forEach((listener) => listener(memoryState));
}

export function toast(opts: Omit<ToastItem, "id"> & { duration?: number }) {
  const id = genId();
  dispatch([{ ...opts, id }, ...memoryState.toasts].slice(0, 5));
  if (opts.duration !== Infinity) {
    setTimeout(() => {
      dispatch(memoryState.toasts.filter((t) => t.id !== id));
    }, opts.duration ?? 5000);
  }
  return id;
}

export function useToast() {
  const [state, setState] = React.useState<ToastState>(memoryState);
  React.useEffect(() => {
    listeners.push(setState);
    return () => {
      const index = listeners.indexOf(setState);
      if (index > -1) listeners.splice(index, 1);
    };
  }, []);
  return { ...state, toast, dismiss: (id: string) => dispatch(state.toasts.filter((t) => t.id !== id)) };
}
