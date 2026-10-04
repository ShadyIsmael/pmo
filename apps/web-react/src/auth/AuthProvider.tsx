import * as React from "react";
import { api, clearTokens, hasTokens, setTokens, type CurrentUser } from "../api/client";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";
interface AuthContextValue { status: AuthStatus; user: CurrentUser | null; isAuthenticated: boolean; login: (email: string, password: string) => Promise<void>; logout: () => Promise<void>; }
const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    if (!hasTokens()) { setStatus("unauthenticated"); return () => { cancelled = true; }; }
    api.get<CurrentUser>("/api/auth/me").then((nextUser) => { if (!cancelled) { setUser(nextUser); setStatus("authenticated"); } }).catch(() => { if (!cancelled) { clearTokens(); setStatus("unauthenticated"); } });
    return () => { cancelled = true; };
  }, []);

  const login = React.useCallback(async (email: string, password: string) => { const response = await api.login(email, password); setTokens(response.token, response.refreshToken); setUser(response.user); setStatus("authenticated"); }, []);
  const logout = React.useCallback(async () => { const token = window.localStorage.getItem("nexus_refresh_token"); try { await api.logout(token); } catch { /* Local session is still cleared. */ } clearTokens(); setUser(null); setStatus("unauthenticated"); }, []);
  const value = React.useMemo(() => ({ status, user, isAuthenticated: status === "authenticated", login, logout }), [status, user, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue { const value = React.useContext(AuthContext); if (!value) throw new Error("useAuth must be used within AuthProvider"); return value; }
