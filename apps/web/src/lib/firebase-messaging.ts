import { config } from "@/lib/config";
import { notificationApi } from "@/lib/api/notifications";

const DEVICE_ID_KEY = "nexus_fcm_device_id";
const ENABLED_KEY = "nexus_fcm_enabled";
const FIREBASE_VERSION = "12.19.0";

const firebaseConfig = {
  apiKey: config.firebase.apiKey,
  authDomain: config.firebase.authDomain,
  projectId: config.firebase.projectId,
  storageBucket: config.firebase.storageBucket,
  messagingSenderId: config.firebase.messagingSenderId,
  appId: config.firebase.appId,
};

interface FirebaseMessagingCompat {
  getToken(options: { vapidKey: string; serviceWorkerRegistration: ServiceWorkerRegistration }): Promise<string>;
  onMessage(handler: (payload: MessagePayload) => void): () => void;
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

export interface MessagePayload {
  notification?: { title?: string; body?: string };
  data?: Record<string, string | undefined>;
}

let firebasePromise: Promise<FirebaseCompat | null> | null = null;

export type PushRegistrationStatus =
  | "enabled"
  | "denied"
  | "unsupported"
  | "unconfigured"
  | "unavailable";

export interface PushRegistrationResult {
  status: PushRegistrationStatus;
  deviceId?: string;
}

export function isFirebaseMessagingConfigured(): boolean {
  return Object.values(firebaseConfig).every(Boolean) && Boolean(config.firebase.vapidKey);
}

export function getRegisteredDeviceId(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(DEVICE_ID_KEY);
}

export async function enablePushNotifications(): Promise<PushRegistrationResult> {
  if (
    typeof window === "undefined" ||
    !("Notification" in window) ||
    !("serviceWorker" in navigator)
  ) {
    return { status: "unsupported" };
  }

  if (!isFirebaseMessagingConfigured()) {
    return { status: "unconfigured" };
  }

  const permission =
    Notification.permission === "default"
      ? await Notification.requestPermission()
      : Notification.permission;
  if (permission !== "granted") {
    return { status: "denied" };
  }

  const firebase = await loadFirebaseMessaging();
  if (!firebase) {
    return { status: "unavailable" };
  }

  try {
    const serviceWorkerRegistration = await navigator.serviceWorker.register(
      "/firebase-messaging-sw.js"
    );
    const token = await firebase.messaging().getToken({
      vapidKey: config.firebase.vapidKey,
      serviceWorkerRegistration,
    });
    if (!token) {
      return { status: "unavailable" };
    }

    const device = await notificationApi.registerDevice({
      token,
      platform: "web",
      deviceName: typeof navigator.platform === "string" ? navigator.platform : undefined,
    });
    window.localStorage.setItem(DEVICE_ID_KEY, device.id);
    window.localStorage.setItem(ENABLED_KEY, "true");
    return { status: "enabled", deviceId: device.id };
  } catch {
    return { status: "unavailable" };
  }
}

export async function deactivateRegisteredPushDevice(): Promise<void> {
  const deviceId = getRegisteredDeviceId();
  if (!deviceId) return;

  try {
    await notificationApi.unregisterDevice(deviceId);
  } catch {
    // Local cleanup still happens when the session is already unavailable.
  } finally {
    window.localStorage.removeItem(DEVICE_ID_KEY);
    window.localStorage.removeItem(ENABLED_KEY);
  }
}

export function subscribeToForegroundMessages(
  handler: (payload: MessagePayload) => void
): () => void {
  let unsubscribe: () => void = () => undefined;

  if (
    typeof window !== "undefined" &&
    "Notification" in window &&
    Notification.permission === "granted" &&
    window.localStorage.getItem(ENABLED_KEY) === "true"
  ) {
    void loadFirebaseMessaging().then((firebase) => {
      if (firebase) {
        unsubscribe = firebase.messaging().onMessage(handler);
      }
    });
  }

  return () => unsubscribe();
}

function loadFirebaseMessaging(): Promise<FirebaseCompat | null> {
  if (typeof window === "undefined" || !isFirebaseMessagingConfigured()) {
    return Promise.resolve(null);
  }

  if (!firebasePromise) {
    firebasePromise = new Promise<FirebaseCompat | null>((resolve) => {
      const finish = () => {
        const firebase = window.firebase;
        if (!firebase) {
          resolve(null);
          return;
        }
        if (firebase.apps.length === 0) {
          firebase.initializeApp(firebaseConfig);
        }
        resolve(firebase);
      };

      if (window.firebase) {
        finish();
        return;
      }

      const appScript = document.createElement("script");
      const messagingScript = document.createElement("script");
      appScript.src = `https://www.gstatic.com/firebasejs/${FIREBASE_VERSION}/firebase-app-compat.js`;
      messagingScript.src = `https://www.gstatic.com/firebasejs/${FIREBASE_VERSION}/firebase-messaging-compat.js`;
      appScript.onload = () => {
        messagingScript.onload = finish;
        messagingScript.onerror = () => resolve(null);
        document.head.appendChild(messagingScript);
      };
      appScript.onerror = () => resolve(null);
      document.head.appendChild(appScript);
    }).catch(() => null);
  }

  return firebasePromise;
}
