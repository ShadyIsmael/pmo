import * as React from "react";
import { BrowserRouter } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth/AuthProvider";
import { ManagementProvider } from "./api/management";
import { notificationKeys } from "./api/notifications";
import { I18nProvider } from "./i18n/messages";
import { subscribeToForegroundMessages } from "./lib/firebase-messaging";
import { ThemeProvider } from "./theme/ThemeProvider";
import { AppRoutes } from "./routes/AppRoutes";
import { Toaster } from "./components/ui/Toaster";
import { toast } from "./hooks/use-toast";
import "./index.css";
import "./styles.css";

export function App() {
  return (
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
      <Toaster />
    </ThemeProvider>
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
  const { user, sessionId } = useAuth();
  return <ManagementProvider key={sessionId ?? "unauthenticated"} user={user} sessionId={sessionId}>{children}</ManagementProvider>;
}
