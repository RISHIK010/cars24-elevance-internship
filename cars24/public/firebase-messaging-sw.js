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

if (config && firebase.apps.length === 0) {
  firebase.initializeApp(config);
}

const messaging = firebase.messaging();

// Background messages: the backend sends data-only payloads, so we render the
// notification ourselves and attach the payload for click navigation.
messaging.onBackgroundMessage((payload) => {
  const data = (payload && payload.data) || {};
  const title = data.title || "Cars24";
  const body = data.body || "";

  self.registration.showNotification(title, {
    body,
    icon: "/favicon.ico",
    badge: "/favicon.ico",
    data,
  });
});

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
    firebase.initializeApp(event.data.config);
  }
});
