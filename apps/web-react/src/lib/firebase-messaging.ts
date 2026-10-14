import { notificationApi } from "@api/notifications";

// Native web push for the React/Vite SPA. Public Firebase web settings are
// supplied at build time; Admin/service-account credentials never belong here.
const FIREBASE_VERSION = "12.19.0";
const DEVICE_ID_KEY = "nexus_fcm_device_id";
const ENABLED_KEY = "nexus_fcm_enabled";

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? "",
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? "",
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? "",
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET ?? "",
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? "",
  appId: import.meta.env.VITE_FIREBASE_APP_ID ?? "",
};
const vapidKey = import.meta.env.VITE_FIREBASE_VAPID_KEY ?? "";

export interface ForegroundPayload {
  notification?: { title?: string; body?: string };
  data?: Record<string, string>;
}

interface FirebaseMessagingCompat {
  getToken(options: { vapidKey: string; serviceWorkerRegistration: ServiceWorkerRegistration }): Promise<string>;
}

interface FirebaseCompat {
  apps: unknown[];
  initializeApp(options: typeof firebaseConfig): unknown;
  messaging(): FirebaseMessagingCompat;
}

declare global {
  interface Window {
    firebase?: FirebaseCompat;
  }
}

export type PushRegistrationStatus = "enabled" | "denied" | "unsupported" | "unconfigured" | "unavailable";
export interface PushRegistrationResult { status: PushRegistrationStatus; deviceId?: string; }

let firebasePromise: Promise<FirebaseCompat | null> | null = null;

export function isFirebaseMessagingConfigured(): boolean {
  return Object.values(firebaseConfig).every(Boolean) && Boolean(vapidKey);
}

export function isPushDeviceRegistered(): boolean {
  return typeof window !== "undefined" && window.localStorage.getItem(ENABLED_KEY) === "true";
}

export async function enablePushNotifications(): Promise<PushRegistrationResult> {
  if (typeof window === "undefined" || !("Notification" in window) || !("serviceWorker" in navigator) || !("PushManager" in window)) {
    return { status: "unsupported" };
  }
  if (!isFirebaseMessagingConfigured()) return { status: "unconfigured" };

  let permission: NotificationPermission;
  try {
    permission = Notification.permission === "default" ? await Notification.requestPermission() : Notification.permission;
  } catch {
    return { status: "unavailable" };
  }
  if (permission !== "granted") return { status: "denied" };

  try {
    const firebase = await loadFirebaseMessaging();
    if (!firebase) return { status: "unavailable" };

    const serviceWorkerRegistration = await navigator.serviceWorker.register("/firebase-messaging-sw.js");
    const token = await firebase.messaging().getToken({ vapidKey, serviceWorkerRegistration });
    if (!token) return { status: "unavailable" };

    const device = await notificationApi.registerDevice({
      token,
      platform: "web",
      deviceName: typeof navigator.platform === "string" ? navigator.platform : undefined,
      previousDeviceId: window.localStorage.getItem(DEVICE_ID_KEY) ?? undefined,
    });
    window.localStorage.setItem(DEVICE_ID_KEY, device.id);
    window.localStorage.setItem(ENABLED_KEY, "true");
    return { status: "enabled", deviceId: device.id };
  } catch {
    return { status: "unavailable" };
  }
}

/** Reconcile an existing opted-in device once after authentication without requesting permission. */
export async function reconcileRegisteredPushDevice(): Promise<void> {
  if (typeof window === "undefined" || !isPushDeviceRegistered() || !isFirebaseMessagingConfigured()) return;
  if (!("Notification" in window) || Notification.permission !== "granted" || !("serviceWorker" in navigator)) return;
  try {
    const firebase = await loadFirebaseMessaging();
    if (!firebase) return;
    const registration = await navigator.serviceWorker.register("/firebase-messaging-sw.js");
    const token = await firebase.messaging().getToken({ vapidKey, serviceWorkerRegistration: registration });
    if (!token) return;
    const device = await notificationApi.registerDevice({
      token,
      platform: "web",
      deviceName: typeof navigator.platform === "string" ? navigator.platform : undefined,
      previousDeviceId: window.localStorage.getItem(DEVICE_ID_KEY) ?? undefined,
    });
    window.localStorage.setItem(DEVICE_ID_KEY, device.id);
  } catch {
    // Explicit enablement or the next authenticated app start retries reconciliation.
  }
}

export async function deactivateRegisteredPushDevice(): Promise<void> {
  if (typeof window === "undefined") return;
  const deviceId = window.localStorage.getItem(DEVICE_ID_KEY);
  try {
    if (deviceId) await notificationApi.unregisterDevice(deviceId);
  } catch {
    // Local session cleanup still proceeds if the API session has expired.
  } finally {
    window.localStorage.removeItem(DEVICE_ID_KEY);
    window.localStorage.removeItem(ENABLED_KEY);
  }
}

export function subscribeToForegroundMessages(onMessage: (payload: ForegroundPayload) => void): () => void {
  if (typeof navigator === "undefined" || !navigator.serviceWorker) return () => undefined;

  let active = true;
  const handler = (event: MessageEvent) => {
    const data = event.data as { type?: string; payload?: ForegroundPayload } | null;
    if (active && data?.type === "FCM_MESSAGE" && data.payload) onMessage(data.payload);
  };
  navigator.serviceWorker.addEventListener("message", handler);

  return () => {
    active = false;
    navigator.serviceWorker.removeEventListener("message", handler);
  };
}

function loadFirebaseMessaging(): Promise<FirebaseCompat | null> {
  if (typeof window === "undefined" || !isFirebaseMessagingConfigured()) return Promise.resolve(null);
  if (window.firebase) return Promise.resolve(initializeFirebase(window.firebase));
  if (firebasePromise) return firebasePromise;

  const attempt = new Promise<FirebaseCompat | null>((resolve) => {
    const finish = () => {
      if (!window.firebase) { resolve(null); return; }
      resolve(initializeFirebase(window.firebase));
    };
    const messagingScript = document.createElement("script");
    messagingScript.src = `https://www.gstatic.com/firebasejs/${FIREBASE_VERSION}/firebase-messaging-compat.js`;
    messagingScript.onload = finish;
    messagingScript.onerror = () => resolve(null);

    const appScript = document.createElement("script");
    appScript.src = `https://www.gstatic.com/firebasejs/${FIREBASE_VERSION}/firebase-app-compat.js`;
    appScript.onload = () => document.head.appendChild(messagingScript);
    appScript.onerror = () => resolve(null);
    document.head.appendChild(appScript);
  }).catch(() => null);
  firebasePromise = attempt.then((firebase) => {
    if (!firebase) firebasePromise = null;
    return firebase;
  });
  return firebasePromise;
}

function initializeFirebase(firebase: FirebaseCompat): FirebaseCompat {
  if (firebase.apps.length === 0) firebase.initializeApp(firebaseConfig);
  return firebase;
}
