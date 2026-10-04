/* NEXUS PMO — Firebase Cloud Messaging service worker (Vite SPA, no Next.js dependency).
 * Served from /firebase-messaging-sw.js. Forwards background messages to open
 * clients via postMessage({ type: 'FCM_MESSAGE', payload }) so the React
 * foreground bridge (lib/firebase-messaging.ts) can toast + invalidate queries.
 * API routes must continue going to the .NET backend; SPA fallback must never
 * intercept /api/*.
 */
self.addEventListener("push", (event) => {
  let payload = {};
  try {
    payload = event.data ? event.data.json() : {};
  } catch {
    payload = {};
  }
  const title =
    (payload.notification && payload.notification.title) ||
    (payload.data && payload.data.title) ||
    "NEXUS notification";
  const options = {
    body:
      (payload.notification && payload.notification.body) ||
      (payload.data && payload.data.body) ||
      "",
    data: payload.data || {},
  };
  event.waitUntil(
    (async () => {
      const clients = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      for (const client of clients) {
        client.postMessage({ type: "FCM_MESSAGE", payload });
      }
      return self.registration.showNotification(title, options);
    })()
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil(
    (async () => {
      const clients = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      if (clients.length > 0) {
        await clients[0].focus();
      } else {
        await self.clients.openWindow("/");
      }
    })()
  );
});
