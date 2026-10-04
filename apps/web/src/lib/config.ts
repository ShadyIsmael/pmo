function requireApiUrl(): string {
  const raw = process.env.NEXT_PUBLIC_API_URL;

  if (!raw) {
    throw new Error(
      "NEXT_PUBLIC_API_URL is not set. Add it to apps/web/.env.local, e.g. NEXT_PUBLIC_API_URL=http://localhost:5000"
    );
  }

  if (!/^https?:\/\/[^\s/]+/i.test(raw)) {
    throw new Error(
      `NEXT_PUBLIC_API_URL must be an absolute http(s) URL. Got: "${raw}"`
    );
  }

  return raw.replace(/\/+$/, "");
}

export const config = {
  apiBaseUrl: requireApiUrl(),
  defaultPageSize: Number(process.env.NEXT_PUBLIC_DEFAULT_PAGE_SIZE ?? 20),
  projectDetailPreviewSize: Number(process.env.NEXT_PUBLIC_PROJECT_DETAIL_PREVIEW_SIZE ?? 5),
  defaultLocale: (process.env.NEXT_PUBLIC_DEFAULT_LOCALE ?? "en") as "en",
  supportedLocales: ["en", "ar"] as const,
  queryStaleTime: Number(process.env.NEXT_PUBLIC_QUERY_STALE_TIME ?? 30_000),
  features: { arabic: process.env.NEXT_PUBLIC_FEATURE_ARABIC === "true" },
  firebase: {
    apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY ?? "",
    authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN ?? "",
    projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID ?? "",
    storageBucket: process.env.NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET ?? "",
    messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID ?? "",
    appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID ?? "",
    vapidKey: process.env.NEXT_PUBLIC_FIREBASE_VAPID_KEY ?? "",
  },
} as const;
