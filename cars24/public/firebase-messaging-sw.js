/* eslint-disable */
// Firebase Cloud Messaging service worker.
// Served from the site root (public/). The public Firebase web config is
// passed via the registration URL query string so the worker can initialize
// itself on (re)start, including for background delivery with the tab closed.

importScripts("https://www.gstatic.com/firebasejs/12.19.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/12.19.0/firebase-messaging-compat.js");

function getConfigFromUrl() {
  try {
    const params = new URLSearchParams(self.location.search);
    const raw = params.get("config");
    return raw ? JSON.parse(decodeURIComponent(raw)) : null;
  } catch (error) {
    return null;
  }
}

const config = getConfigFromUrl();

// Narrow diagnostics (temporary): establish whether a push reaches this worker
// and whether the background handler reaches showNotification(). No tokens,
// credentials, or private keys are logged.
console.info("[Cars24 FCM] service worker started", {
  hasConfig: !!config,
  scope: typeof self.registration !== "undefined" ? self.registration.scope : null,
});

// --- Shared notification helpers -------------------------------------------
// Tracks push events already displayed so the Firebase SDK background handler
// and the raw-payload fallback below can never render two notifications for
// one message. The dedupe key is the unique per-push tag (not the content),
// so a later, distinct push with an identical title/body is still shown.
const shownPayloadTags = new Set();

// Each push gets a fresh, unique tag. Using the browser's default (empty) tag
// would silently replace the previous notification instead of raising a new
// Windows/Chrome banner, because renotify defaults to false.
let pushSequence = 0;
let lastPushTag = null;

function makeUniqueTag() {
  pushSequence += 1;
  return "cars24-" + Date.now() + "-" + pushSequence;
}

function showNotificationOnce(data, source, tag) {
  const safeData = data && typeof data === "object" ? data : {};

  if (shownPayloadTags.has(tag)) {
    console.info("[Cars24 FCM] duplicate notification suppressed", { source });
    return Promise.resolve(false);
  }

  // Bound memory over a long-lived worker. The set only needs to deduplicate
  // messages being processed for the same push event.
  if (shownPayloadTags.size >= 100) {
    shownPayloadTags.clear();
  }
  shownPayloadTags.add(tag);

  const title = safeData.title || "Cars24";
  const body = safeData.body || "";

  console.info("[Cars24 FCM] showNotification", {
    source,
    hasTitle: !!safeData.title,
    hasBody: !!safeData.body,
  });

  return self.registration
    .showNotification(title, {
      body,
      tag,
      icon: "/favicon.ico",
      badge: "/favicon.ico",
      data: {
        actionUrl: safeData.actionUrl || "/",
        type: safeData.type || "",
        vehicleId: safeData.vehicleId || "",
        title,
        body,
      },
    })
    .then(() => {
      console.info("[Cars24 FCM] showNotification resolved");
      return true;
    })
    .catch((error) => {
      console.error("[Cars24 FCM] showNotification failed", error);
      throw error;
    });
}

// Raw-payload fallback. Registered before Firebase's own push listener so a
// data-only message is still rendered even if Firebase initialization fails or
// its background handler does not fire for this payload. Notification messages
// (those carrying a `notification` object) are left to the browser/Firebase SDK
// so they are not displayed twice.
self.addEventListener("push", (event) => {
  const pushTag = makeUniqueTag();
  lastPushTag = pushTag;

  let payload = null;
  try {
    payload = event.data ? event.data.json() : null;
  } catch (error) {
    payload = null;
  }

  console.info("[Cars24 FCM] push event received", {
    hasData: !!event.data,
    hasPayload: !!payload,
    hasNotification: !!(payload && payload.notification),
  });

  if (!payload || payload.notification) {
    return;
  }

  // Data-only payloads arrive as { data: { ... } } (FCM envelope) or, from some
  // senders, as the data object at the top level.
  const data =
    payload.data && typeof payload.data === "object" && !Array.isArray(payload.data)
      ? payload.data
      : payload;

  if (!data || typeof data !== "object" || (!data.title && !data.body)) {
    return;
  }

  event.waitUntil(
    (async () => {
      // Mirror the SDK: when a page is focused the foreground onMessage handler
      // (in-app toast) is the single notification surface, so do not also raise
      // a system notification.
      try {
        const clientList = await self.clients.matchAll({
          type: "window",
          includeUncontrolled: true,
        });
        const hasVisibleClient = clientList.some(
          (client) =>
            client.visibilityState === "visible" &&
            !client.url.startsWith("chrome-extension://")
        );
        if (hasVisibleClient) {
          return;
        }
      } catch (error) {
        // Ignore: if visibility cannot be determined, show the notification.
      }

      await showNotificationOnce(data, "fallback", pushTag);
    })().catch(() => {
      // showNotificationOnce already logs failures; avoid an unhandled
      // rejection inside waitUntil.
    })
  );
});

if (config && firebase.apps.length === 0) {
  try {
    firebase.initializeApp(config);
  } catch (error) {
    console.error("[Cars24 FCM] initializeApp failed", error);
  }
}

let messaging = null;
try {
  messaging = firebase.messaging();
} catch (error) {
  console.error("[Cars24 FCM] firebase.messaging() failed", error);
}

// Background messages: the backend sends data-only payloads, so we render the
// notification ourselves and attach the payload for click navigation. This path
// shares the per-push dedupe tag with the raw-payload fallback above.
if (messaging) {
  messaging.onBackgroundMessage((payload) => {
    const data = (payload && payload.data) || {};
    const tag = lastPushTag || makeUniqueTag();

    console.info("[Cars24 FCM] onBackgroundMessage called", {
      hasData: !!data,
      hasTitle: !!data.title,
      hasBody: !!data.body,
    });

    return showNotificationOnce(data, "sdk", tag);
  });
}

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const data = event.notification.data || {};
  const actionUrl =
    data.actionUrl ||
    (data.FCM_MSG && data.FCM_MSG.data && data.FCM_MSG.data.actionUrl) ||
    "/";

  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((windowClients) => {
        for (const client of windowClients) {
          if ("navigate" in client) {
            return client.navigate(actionUrl);
          }
        }
        return self.clients.openWindow(actionUrl);
      })
  );
});

// Fallback: allow the main app to (re)initialize the worker if it was started
// without a config query string.
self.addEventListener("message", (event) => {
  if (
    event.data &&
    event.data.type === "FCM_INIT" &&
    event.data.config &&
    firebase.apps.length === 0
  ) {
    try {
      firebase.initializeApp(event.data.config);
    } catch (error) {
      console.error("[Cars24 FCM] FCM_INIT initializeApp failed", error);
    }
  }
});
