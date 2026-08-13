import * as React from "react";

type ToastVariant = "default" | "destructive" | "success";

interface Toast {
  id: string;
  title?: string;
  description?: string;
  action?: React.ReactNode;
  variant?: ToastVariant;
}

interface ToastState {
  toasts: Toast[];
}

type ToastAction =
  | { type: "ADD_TOAST"; toast: Toast }
  | { type: "DISMISS_TOAST"; toastId: string }
  | { type: "REMOVE_TOAST"; toastId: string };

let count = 0;

function genId() {
  count = (count + 1) % Number.MAX_SAFE_INTEGER;
  return count.toString();
}

function toastReducer(state: ToastState, action: ToastAction): ToastState {
  switch (action.type) {
    case "ADD_TOAST":
      return { ...state, toasts: [action.toast, ...state.toasts] };
    case "DISMISS_TOAST":
      return {
        ...state,
        toasts: state.toasts.map((t) =>
          t.id === action.toastId ? { ...t } : t
        ),
      };
    case "REMOVE_TOAST":
      return {
        ...state,
        toasts: state.toasts.filter((t) => t.id !== action.toastId),
      };
    default:
      return state;
  }
}

const listeners: Array<(_state: ToastState) => void> = [];
let memoryState: ToastState = { toasts: [] };

function dispatch(action: ToastAction) {
  memoryState = toastReducer(memoryState, action);
  listeners.forEach((listener) => listener(memoryState));
}

interface ToastOptions {
  title?: string;
  description?: string;
  action?: React.ReactNode;
  variant?: ToastVariant;
  duration?: number;
}

function toast(opts: ToastOptions) {
  const id = genId();
  const dismiss = () => dispatch({ type: "DISMISS_TOAST", toastId: id });
  const remove = () => dispatch({ type: "REMOVE_TOAST", toastId: id });

  dispatch({
    type: "ADD_TOAST",
    toast: { ...opts, id },
  });

  if (opts.duration !== Infinity) {
    setTimeout(() => {
      dismiss();
      setTimeout(remove, 300);
    }, opts.duration ?? 5000);
  }

  return { id, dismiss, remove };
}

function useToast() {
  const [state, setState] = React.useState<ToastState>(memoryState);

  React.useEffect(() => {
    listeners.push(setState);
    return () => {
      const index = listeners.indexOf(setState);
      if (index > -1) listeners.splice(index, 1);
    };
  }, []);

  return {
    ...state,
    toast,
    dismiss: (toastId: string) => dispatch({ type: "DISMISS_TOAST", toastId }),
    remove: (toastId: string) => dispatch({ type: "REMOVE_TOAST", toastId }),
  };
}

export { useToast, toast };
