"use client";

import * as React from "react";
import { ThemeProvider } from "next-themes";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Toaster } from "@/components/ui/toaster";
import { QUERY_STALE_TIME } from "@/lib/constants";
import { AuthProvider } from "@/lib/auth";
import { ApiError } from "@/lib/api/client";
import { ManagementPerspectiveBridge } from "@/lib/perspective-context";

function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403 || (error.status >= 400 && error.status < 500)) return false;
  }
  return failureCount < 1;
}

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = React.useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: QUERY_STALE_TIME,
            retry: shouldRetry,
            refetchOnWindowFocus: false,
          },
          mutations: {
            retry: shouldRetry,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider
        attribute="class"
        defaultTheme="system"
        enableSystem
        disableTransitionOnChange
      >
        <AuthProvider>
          <ManagementPerspectiveBridge>
            <TooltipProvider delayDuration={300}>{children}</TooltipProvider>
          </ManagementPerspectiveBridge>
        </AuthProvider>
        <Toaster />
      </ThemeProvider>
    </QueryClientProvider>
  );
}

