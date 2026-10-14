import * as React from "react";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth/AuthProvider";
import { ManagementProvider } from "./api/management";
import { notificationKeys } from "./api/notifications";
import { I18nProvider } from "./i18n/messages";
import { subscribeToForegroundMessages } from "./lib/firebase-messaging";
import { ThemeProvider } from "./theme/ThemeProvider";
import { AppRoutes } from "./routes/AppRoutes";
import { ApiError } from "./api/client";
import { Toaster } from "./components/ui/Toaster";
import { toast } from "./hooks/use-toast";
import "./index.css";
import "./styles.css";

function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403 || (error.status >= 400 && error.status < 500)) return false;
  }
  return failureCount < 1;
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: shouldRetry, refetchOnWindowFocus: false },
    mutations: { retry: shouldRetry },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <I18nProvider>
          <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
            <AuthProvider>
              <FirebaseMessagingBridge />
              <ManagementBridge>
                <AppRoutes />
              </ManagementBridge>
            </AuthProvider>
          </BrowserRouter>
        </I18nProvider>
      </ThemeProvider>
      <Toaster />
    </QueryClientProvider>
  );
}

function FirebaseMessagingBridge() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  React.useEffect(() => {
    if (!user) return undefined;
    return subscribeToForegroundMessages((payload) => {
      toast({
        title: payload.notification?.title ?? payload.data?.title ?? "NEXUS notification",
        description: payload.notification?.body ?? payload.data?.body,
      });
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    });
  }, [queryClient, user]);

  return null;
}

function ManagementBridge({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  return <ManagementProvider user={user}>{children}</ManagementProvider>;
}
