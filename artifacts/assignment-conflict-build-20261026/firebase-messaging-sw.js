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
      let hasVisibleClient = false;
      for (const client of clients) {
        client.postMessage({ type: "FCM_MESSAGE", payload });
        if (client.visibilityState === "visible") hasVisibleClient = true;
      }
      if (hasVisibleClient) return;
      return self.registration.showNotification(title, options);
    })()
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil(
    (async () => {
      const data = event.notification.data || {};
      const target = resolveNotificationTarget(data.referenceType, data.referenceId);
      const clients = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      if (clients.length > 0) {
        const client = clients.find((candidate) => new URL(candidate.url).origin === self.location.origin) || clients[0];
        if (target && "navigate" in client) await client.navigate(target);
        await client.focus();
      } else {
        await self.clients.openWindow(target || "/");
      }
    })()
  );
});

function resolveNotificationTarget(referenceType, referenceId) {
  if (typeof referenceType !== "string" || typeof referenceId !== "string"
      || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(referenceId)) return null;
  const id = encodeURIComponent(referenceId);
  switch (referenceType.toLowerCase()) {
    case "project": return `/projects/${id}`;
    case "program": return `/portfolios/programs/${id}`;
    case "portfolio": return `/portfolios/${id}`;
    default: return null;
  }
}
