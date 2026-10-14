# Firebase Cloud Messaging

NEXUS persists notifications in its database as the source of truth. FCM is an asynchronous delivery channel: missing Firebase credentials or provider outages must not make a business transaction fail. The delivery worker records send outcomes for later inspection/retry.

Real-time browser delivery uses FCM only; this integration does not add SignalR or another WebSocket layer. In the open React app, service-worker messages trigger a toast and refresh the Notification Centre. In the background, the service worker displays the browser notification.

## Firebase project setup

Use a Firebase project dedicated to NEXUS; do not reuse the fopx project.

1. Add a Web App to the NEXUS Firebase project and enable Cloud Messaging.
2. Generate a Web Push VAPID key pair. The public VAPID key is used by the browser.
3. Configure Firebase Admin credentials for the API using a secret manager, mounted secret file, or application-default/workload identity. Never commit or paste a service-account private key into source control or chat.
4. Serve the React app over HTTPS in deployed environments. Localhost is a secure context for development.

## React web configuration

Provide these build-time variables to `apps/web-react`. Firebase web configuration and the VAPID public key are public client values; they are not substitutes for API credentials.

```text
VITE_FIREBASE_API_KEY
VITE_FIREBASE_AUTH_DOMAIN
VITE_FIREBASE_PROJECT_ID
VITE_FIREBASE_STORAGE_BUCKET
VITE_FIREBASE_MESSAGING_SENDER_ID
VITE_FIREBASE_APP_ID
VITE_FIREBASE_VAPID_KEY
```

The service worker is served from `/firebase-messaging-sw.js`. Notification permission is requested only after the user explicitly enables push from the Notification Centre. The resulting FCM token is registered against the authenticated tenant/user and current device; logging out deactivates that device registration rather than other devices.

## API configuration

Use environment-variable mapping or deployment secrets for the API settings below. The names are configuration keys, not credential values.

```text
Firebase__ProjectId=<dedicated-nexus-project-id>
Firebase__CredentialsPath=<mounted-secret-file-path>
Firebase__CredentialsJson=<secret-manager-injected-service-account-json>
Firebase__AllowApplicationDefaultCredentials=<true-or-false>
```

Choose the credentials mechanism appropriate to the deployment; do not set or commit a private key in `appsettings.json`. When Firebase sending is unavailable, persisted notifications remain available in the application.
