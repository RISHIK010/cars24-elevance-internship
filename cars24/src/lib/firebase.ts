import type { FirebaseApp } from "firebase/app";
import type { MessagePayload } from "firebase/messaging";

// Browser-safe Firebase Cloud Messaging (FCM) helpers.
// No Firebase SDK is imported at module load time, so this module can be
// imported into components that render on the server. All browser APIs are
// only touched inside functions that execute in the client.

export type FirebaseWebConfig = {
  apiKey: string;
  authDomain: string;
  projectId: string;
  storageBucket: string;
  messagingSenderId: string;
  appId: string;
  vapidKey: string;
};

export function getFirebaseConfig(): FirebaseWebConfig | null {
  const config: FirebaseWebConfig = {
    apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY ?? "",
    authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN ?? "",
    projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID ?? "",
    storageBucket: process.env.NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET ?? "",
    messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID ?? "",
    appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID ?? "",
    vapidKey: process.env.NEXT_PUBLIC_FIREBASE_VAPID_KEY ?? "",
  };

  if (
    !config.apiKey ||
    !config.projectId ||
    !config.appId ||
    !config.messagingSenderId ||
    !config.vapidKey
  ) {
    return null;
  }
  return config;
}

export function isFirebaseConfigured(): boolean {
  return getFirebaseConfig() !== null;
}

export function isNotificationSupported(): boolean {
  if (typeof window === "undefined") return false;
  return (
    "Notification" in window &&
    "serviceWorker" in navigator &&
    "PushManager" in window
  );
}

export function getCurrentPermission(): NotificationPermission | null {
  if (typeof window === "undefined" || !("Notification" in window)) return null;
  return Notification.permission;
}

const DEVICE_ID_KEY = "cars24-device-id";

export function getDeviceId(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(DEVICE_ID_KEY);
}

function getOrCreateDeviceId(): string {
  const existing = getDeviceId();
  if (existing) return existing;
  const id =
    typeof crypto !== "undefined" && "randomUUID" in crypto
      ? crypto.randomUUID()
      : `dev-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  localStorage.setItem(DEVICE_ID_KEY, id);
  return id;
}

let appPromise: Promise<FirebaseApp> | null = null;

async function getApp(): Promise<FirebaseApp> {
  if (!appPromise) {
    appPromise = (async () => {
      const config = getFirebaseConfig();
      if (!config) throw new Error("Firebase is not configured");
      const { initializeApp } = await import("firebase/app");
      return initializeApp(config);
    })();
  }
  return appPromise;
}

async function acquireRegistrationToken(): Promise<{
  token: string;
  deviceId: string;
}> {
  const app = await getApp();
  const { getMessaging, getToken, isSupported } = await import(
    "firebase/messaging"
  );
  if (!(await isSupported()))
    throw new Error("Messaging is not supported in this browser");

  const config = getFirebaseConfig()!;
  const swUrl = `/firebase-messaging-sw.js?config=${encodeURIComponent(
    JSON.stringify(config)
  )}`;
  const registration = await navigator.serviceWorker.register(swUrl);
  await navigator.serviceWorker.ready;

  const messaging = getMessaging(app);
  const token = await getToken(messaging, {
    vapidKey: config.vapidKey,
    serviceWorkerRegistration: registration,
  });

  const deviceId = getOrCreateDeviceId();
  return { token, deviceId };
}

export async function enableNotifications(): Promise<{
  token: string;
  deviceId: string;
}> {
  if (typeof window === "undefined") throw new Error("Not in a browser");
  if (!isNotificationSupported())
    throw new Error("Notifications are not supported in this browser");
  if (!isFirebaseConfigured()) throw new Error("Firebase is not configured");

  const requested = await Notification.requestPermission();
  // requestPermission() can resolve to "granted" while the browser's persisted
  // permission is still "default" (e.g. a quiet/dismissed prompt in Edge). Trust
  // the live permission state, because that is what determines whether the
  // service worker is actually allowed to display a notification.
  const actual = getCurrentPermission();

  if (requested !== "granted" || actual !== "granted") {
    if (requested === "denied" || actual === "denied") {
      throw new Error(
        "Notifications are blocked in your browser. Allow them in your browser settings and try again."
      );
    }
    throw new Error(
      "Notification permission was not granted. Please allow notifications and try again."
    );
  }

  return acquireRegistrationToken();
}

// Refreshes the current FCM token without prompting for permission. Only call
// this when the browser notification permission is already granted; it never
// shows a permission prompt, so it is safe to run during page-load
// reconciliation.
export async function refreshRegistrationToken(): Promise<{
  token: string;
  deviceId: string;
}> {
  if (typeof window === "undefined") throw new Error("Not in a browser");
  if (!isNotificationSupported())
    throw new Error("Notifications are not supported in this browser");
  if (!isFirebaseConfigured()) throw new Error("Firebase is not configured");
  if (getCurrentPermission() !== "granted") {
    throw new Error("Notification permission is not granted.");
  }

  return acquireRegistrationToken();
}

export async function disableNotifications(): Promise<void> {
  if (typeof window === "undefined") return;
  try {
    const app = await getApp();
    const { getMessaging, deleteToken } = await import("firebase/messaging");
    await deleteToken(getMessaging(app));
  } catch {
    // Ignore: token may not exist or Firebase may not be configured.
  }
}

export function onForegroundMessage(
  callback: (payload: MessagePayload) => void
): () => void {
  let unsubscribe: (() => void) | null = null;
  let disposed = false;

  (async () => {
    try {
      const app = await getApp();
      const { getMessaging, onMessage } = await import("firebase/messaging");
      const result: unknown = onMessage(getMessaging(app), callback);
      if (typeof result === "function") {
        unsubscribe = result as () => void;
      } else {
        unsubscribe = (await (result as Promise<() => void>)) ?? null;
      }
      if (disposed) unsubscribe?.();
    } catch {
      // Foreground messaging is optional; ignore setup failures.
    }
  })();

  return () => {
    disposed = true;
    unsubscribe?.();
  };
}
