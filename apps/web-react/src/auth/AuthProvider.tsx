import * as React from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { api, clearTokens, getClientSessionId, getSessionGeneration, hasSession, SessionChangedError, setTokens, syncStoredSession, type CurrentUser } from "../api/client";
import { createSessionQueryClient } from "../api/queryClient";
import { deactivateRegisteredPushDevice, reconcileRegisteredPushDevice } from "../lib/firebase-messaging";

export type AuthStatus = "loading" | "authenticating" | "initializing" | "authenticated" | "unauthenticated";
interface AuthContextValue { status: AuthStatus; user: CurrentUser | null; sessionId: string | null; isAuthenticated: boolean; login: (email: string, password: string) => Promise<CurrentUser>; logout: () => Promise<void>; refreshUser: () => Promise<void>; }
const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);
  const [sessionId, setSessionId] = React.useState<string | null>(getClientSessionId());
  const statusRef = React.useRef(status);
  React.useEffect(() => { statusRef.current = status; }, [status]);
  const [queryClient, setQueryClient] = React.useState(createSessionQueryClient);
  const queryClientRef = React.useRef(queryClient);
  const transitionId = React.useRef(0);
  const authorizationRefreshInFlight = React.useRef(false);

  const resetSessionCache = React.useCallback(() => {
    void queryClientRef.current.cancelQueries();
    queryClientRef.current.clear();
    const next = createSessionQueryClient();
    queryClientRef.current = next;
    setQueryClient(next);
  }, []);

  const initializeCurrentSession = React.useCallback(async (expectedTransition: number, expectedGeneration: number, expectedSessionId: string) => {
    try {
      const nextUser = await api.getMe();
      if (transitionId.current !== expectedTransition || getSessionGeneration() !== expectedGeneration || getClientSessionId() !== expectedSessionId) return;
      setUser(nextUser);
      setSessionId(expectedSessionId);
      setStatus("authenticated");
    } catch (error) {
      if (transitionId.current !== expectedTransition || error instanceof SessionChangedError) return;
      if (getSessionGeneration() === expectedGeneration && getClientSessionId() === expectedSessionId) clearTokens();
      setUser(null);
      setSessionId(null);
      setStatus("unauthenticated");
    }
  }, []);

  React.useEffect(() => {
    if (!hasSession()) { setUser(null); setSessionId(null); setStatus("unauthenticated"); return; }
    const currentSessionId = getClientSessionId();
    if (!currentSessionId) { clearTokens(); setUser(null); setSessionId(null); setStatus("unauthenticated"); return; }
    setUser(null);
    setSessionId(currentSessionId);
    setStatus("initializing");
    const operation = ++transitionId.current;
    const generation = getSessionGeneration();
    void initializeCurrentSession(operation, generation, currentSessionId);
  }, [initializeCurrentSession]);

  React.useEffect(() => {
    if (status === "authenticated" && user?.id) void reconcileRegisteredPushDevice();
  }, [status, user?.id]);

  React.useEffect(() => {
    const expireSession = () => {
      ++transitionId.current;
      clearTokens();
      setUser(null);
      setSessionId(null);
      resetSessionCache();
      setStatus("unauthenticated");
    };
    window.addEventListener("nexus:session-expired", expireSession);
    return () => window.removeEventListener("nexus:session-expired", expireSession);
  }, [resetSessionCache]);

  React.useEffect(() => {
    const synchronizeSession = () => {
      if (!syncStoredSession()) {
        if (statusRef.current === "authenticated" && hasSession()) {
          const expectedSessionId = getClientSessionId();
          const expectedGeneration = getSessionGeneration();
          void api.getMe().then((nextUser) => {
            if (statusRef.current === "authenticated"
              && getSessionGeneration() === expectedGeneration
              && getClientSessionId() === expectedSessionId) setUser(nextUser);
          }).catch(() => {
            // A transient refresh failure leaves the current view intact; authenticated API
            // requests still resolve current permissions on the server.
          });
        }
        return;
      }
      const operation = ++transitionId.current;
      resetSessionCache();
      setUser(null);
      const nextSessionId = getClientSessionId();
      setSessionId(nextSessionId);
      if (!hasSession() || !nextSessionId) {
        setSessionId(null);
        setStatus("unauthenticated");
        return;
      }
      setStatus("initializing");
      const generation = getSessionGeneration();
      void initializeCurrentSession(operation, generation, nextSessionId);
    };
    const onStorage = (event: StorageEvent) => {
      if (event.key === "nexus_auth_session_id" || event.key === null) synchronizeSession();
    };
    const onVisible = () => { if (document.visibilityState === "visible") synchronizeSession(); };
    window.addEventListener("storage", onStorage);
    window.addEventListener("focus", synchronizeSession);
    document.addEventListener("visibilitychange", onVisible);
    const refreshTimer = window.setInterval(synchronizeSession, 15_000);
    return () => {
      window.removeEventListener("storage", onStorage);
      window.removeEventListener("focus", synchronizeSession);
      document.removeEventListener("visibilitychange", onVisible);
      window.clearInterval(refreshTimer);
    };
  }, [initializeCurrentSession, resetSessionCache]);

  const login = React.useCallback(async (email: string, password: string) => {
    const operation = ++transitionId.current;
    clearTokens();
    resetSessionCache();
    setUser(null);
    setSessionId(null);
    setStatus("authenticating");
    try {
      const response = await api.login(email, password);
      if (transitionId.current !== operation) throw new SessionChangedError();
      const nextSessionId = setTokens(response.token);
      const generation = getSessionGeneration();
      resetSessionCache();
      setSessionId(nextSessionId);
      setStatus("initializing");
      const currentUser = await api.getMe();
      if (transitionId.current !== operation || getSessionGeneration() !== generation || getClientSessionId() !== nextSessionId) throw new SessionChangedError();
      setUser(currentUser);
      setStatus("authenticated");
      return currentUser;
    } catch (error) {
      if (transitionId.current === operation) {
        clearTokens();
        resetSessionCache();
        setUser(null);
        setSessionId(null);
        setStatus("unauthenticated");
      }
      throw error;
    }
  }, [resetSessionCache]);

  const logout = React.useCallback(async () => {
    ++transitionId.current;
    const revoke = api.logout((accessToken) => deactivateRegisteredPushDevice(accessToken));
    clearTokens();
    resetSessionCache();
    setUser(null);
    setSessionId(null);
    setStatus("unauthenticated");
    try { await revoke; } catch { /* Local logout remains effective when the server is unavailable. */ }
  }, [resetSessionCache]);

  const refreshUser = React.useCallback(async () => {
    const generation = getSessionGeneration();
    const activeSessionId = getClientSessionId();
    const me = await api.getMe();
    if (!activeSessionId || generation !== getSessionGeneration() || activeSessionId !== getClientSessionId()) throw new SessionChangedError();
    setUser(me);
    setStatus("authenticated");
  }, []);

  React.useEffect(() => {
    const refreshAfterForbidden = () => {
      if (statusRef.current !== "authenticated" || authorizationRefreshInFlight.current) return;
      authorizationRefreshInFlight.current = true;
      // A server-side denial can mean a grant was revoked while this tab was open.
      // Hide permission-gated UI until the authoritative permission set is reloaded.
      setUser((currentUser) => currentUser ? { ...currentUser, permissions: [] } : currentUser);
      void refreshUser()
        .catch(() => undefined)
        .finally(() => { authorizationRefreshInFlight.current = false; });
    };
    window.addEventListener("nexus:authorization-denied", refreshAfterForbidden);
    return () => window.removeEventListener("nexus:authorization-denied", refreshAfterForbidden);
  }, [refreshUser]);

  const value = React.useMemo(() => ({ status, user, sessionId, isAuthenticated: status === "authenticated", login, logout, refreshUser }), [status, user, sessionId, login, logout, refreshUser]);
  return <AuthContext.Provider value={value}><QueryClientProvider client={queryClient}>{children}</QueryClientProvider></AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = React.useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used within AuthProvider");
  return value;
}
