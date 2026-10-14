import * as React from "react";
import { api, clearTokens, hasTokens, setTokens, type CurrentUser } from "../api/client";
import { deactivateRegisteredPushDevice, reconcileRegisteredPushDevice } from "../lib/firebase-messaging";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";
interface AuthContextValue { status: AuthStatus; user: CurrentUser | null; isAuthenticated: boolean; login: (email: string, password: string) => Promise<CurrentUser>; logout: () => Promise<void>; refreshUser: () => Promise<void>; }
const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    if (!hasTokens()) { setStatus("unauthenticated"); return () => { cancelled = true; }; }
    api.getMe().then((nextUser) => { if (!cancelled) { setUser(nextUser); setStatus("authenticated"); } }).catch(() => { if (!cancelled) { clearTokens(); setStatus("unauthenticated"); } });
    return () => { cancelled = true; };
  }, []);

  React.useEffect(() => {
    if (status === "authenticated" && user) void reconcileRegisteredPushDevice();
  }, [status, user?.id]);

  React.useEffect(() => {
    const expireSession = () => {
      clearTokens();
      setUser(null);
      setStatus("unauthenticated");
    };
    window.addEventListener("nexus:session-expired", expireSession);
    return () => window.removeEventListener("nexus:session-expired", expireSession);
  }, []);

  const login = React.useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password);
    setTokens(response.token, response.refreshToken);
    setUser(response.user);
    setStatus("authenticated");
    return response.user;
  }, []);

  const logout = React.useCallback(async () => {
    const token = window.localStorage.getItem("nexus_refresh_token");
    try {
      await deactivateRegisteredPushDevice();
    } catch {
      /* Push-device cleanup must not prevent session revocation. */
    }
    try {
      await api.logout(token);
    } catch {
      /* Local session is still cleared if the API is unavailable. */
    }
    clearTokens();
    setUser(null);
    setStatus("unauthenticated");
  }, []);

  const refreshUser = React.useCallback(async () => {
    const me = await api.getMe();
    setUser(me);
    setStatus("authenticated");
  }, []);

  const value = React.useMemo(() => ({ status, user, isAuthenticated: status === "authenticated", login, logout, refreshUser }), [status, user, login, logout, refreshUser]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = React.useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used within AuthProvider");
  return value;
}
