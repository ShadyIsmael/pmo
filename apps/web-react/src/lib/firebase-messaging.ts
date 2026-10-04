// Native web push for React/Vite SPA — no Next.js dependency.
// Uses a plain service worker at /firebase-messaging-sw.js served from public/.
export interface ForegroundPayload {
  notification?: { title?: string; body?: string };
  data?: Record<string, string>;
}

let supportedCache: boolean | null = null;

async function isSupported(): Promise<boolean> {
  if (supportedCache !== null) return supportedCache;
  try {
    if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
      supportedCache = false;
      return false;
    }
    supportedCache = true;
    return true;
  } catch {
    supportedCache = false;
    return false;
  }
}

export function subscribeToForegroundMessages(onMessage: (payload: ForegroundPayload) => void): () => void {
  let cancelled = false;
  // Vite SPA: listen for service-worker postMessage events (sw posts { type: 'FCM_MESSAGE', payload }).
  const handler = (event: MessageEvent) => {
    if (cancelled) return;
    const data = event.data as { type?: string; payload?: ForegroundPayload } | null;
    if (data?.type === "FCM_MESSAGE" && data.payload) onMessage(data.payload);
  };
  navigator.serviceWorker?.addEventListener("message", handler);
  void isSupported().then((ok) => {
    if (!ok || cancelled) return;
    // Registration itself is done by the SW file; nothing else required for foreground bridge.
  });
  return () => {
    cancelled = true;
    navigator.serviceWorker?.removeEventListener("message", handler);
  };
}

export async function deactivateRegisteredPushDevice(): Promise<void> {
  // Best-effort: backend deactivation happens via logout; SW unregistration is left to browser lifecycle.
  return Promise.resolve();
}
