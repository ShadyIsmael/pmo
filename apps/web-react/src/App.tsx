import * as React from "react";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth/AuthProvider";
import { ManagementProvider } from "./api/management";
import { I18nProvider } from "./i18n/messages";
import { ThemeProvider } from "./theme/ThemeProvider";
import { AppRoutes } from "./routes/AppRoutes";
import { ApiError } from "./api/client";
import { Toaster } from "./components/ui/Toaster";
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
          <BrowserRouter>
            <AuthProvider>
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

function ManagementBridge({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  return <ManagementProvider user={user}>{children}</ManagementProvider>;
}
