import * as React from "react";
import { BrowserRouter } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth/AuthProvider";
import { ManagementProvider } from "./api/management";
import { notificationKeys } from "./api/notifications";
import { useTenantRuntimeSettings } from "./features/administration/api/settings";
import { I18nProvider, useI18n } from "./i18n/messages";
import { subscribeToForegroundMessages } from "./lib/firebase-messaging";
import { brand } from "./lib/brand";
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
        <BrowserRouter>
          <AuthProvider>
            <TenantRuntimeSettingsBridge />
            <FirebaseMessagingBridge />
            <ManagementBridge>
              <AppRoutes />
            </ManagementBridge>
          </AuthProvider>
        </BrowserRouter>
        <Toaster />
      </I18nProvider>
    </ThemeProvider>
  );
}

function TenantRuntimeSettingsBridge() {
  const { user } = useAuth();
  const runtimeSettings = useTenantRuntimeSettings(Boolean(user?.tenantId));
  const { setRuntimeRegionalSettings } = useI18n();

  React.useEffect(() => {
    if (!user?.tenantId || !runtimeSettings.data) {
      setRuntimeRegionalSettings(null);
      return;
    }

    const values = new Map(runtimeSettings.data.settings.map(setting => [setting.key, setting.value]));
    const stringValue = (key: string): string | null => {
      const value = values.get(key);
      return typeof value === "string" ? value : null;
    };
    const defaultLanguage = stringValue("regional.defaultLanguage");
    setRuntimeRegionalSettings({
      defaultLanguage: defaultLanguage === "ar" || defaultLanguage === "en" ? defaultLanguage : null,
      timeZone: stringValue("regional.defaultTimeZone"),
      currency: stringValue("regional.defaultCurrency"),
      dateFormat: stringValue("regional.defaultDateFormat"),
      timeFormat: stringValue("regional.defaultTimeFormat"),
      firstDayOfWeek: stringValue("regional.firstDayOfWeek"),
    });
  }, [runtimeSettings.data, user?.tenantId, setRuntimeRegionalSettings]);

  return null;
}

function FirebaseMessagingBridge() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  React.useEffect(() => {
    if (!user) return undefined;
    return subscribeToForegroundMessages((payload) => {
      toast({
        title: payload.notification?.title ?? payload.data?.title ?? `${brand.product.name} notification`,
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
