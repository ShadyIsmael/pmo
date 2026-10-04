import * as React from "react";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth/AuthProvider";
import { ManagementProvider } from "./api/management";
import { I18nProvider } from "./i18n/messages";
import { ThemeProvider } from "./theme/ThemeProvider";
import { AppRoutes } from "./routes/AppRoutes";
import "./styles.css";

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1 } } });
export function App() { return <QueryClientProvider client={queryClient}><ThemeProvider><I18nProvider><BrowserRouter><AuthProvider><ManagementBridge><AppRoutes /></ManagementBridge></AuthProvider></BrowserRouter></I18nProvider></ThemeProvider></QueryClientProvider>; }

function ManagementBridge({ children }: { children: React.ReactNode }) { const { user } = useAuth(); return <ManagementProvider user={user}>{children}</ManagementProvider>; }
