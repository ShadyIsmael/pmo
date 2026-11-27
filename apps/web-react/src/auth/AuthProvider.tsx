import * as React from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { ApiError, api, clearTokens, getClientSessionId, getSessionGeneration, hasSession, SessionChangedError, setTokens, syncStoredSession, type CurrentUser } from "../api/client";
import { createSessionQueryClient } from "../api/queryClient";
import { deactivateRegisteredPushDevice, reconcileRegisteredPushDevice } from "../lib/firebase-messaging";

export type AuthStatus = "loading" | "authenticating" | "initializing" | "authenticated" | "unauthenticated";
interface AuthContextValue { status: AuthStatus; user: CurrentUser | null; sessionId: string | null; isAuthenticated: boolean; login: (email: string, password: string) => Promise<CurrentUser>; logout: () => Promise<void>; refreshUser: () => Promise<void>; }
const AuthContext = React.createContext<AuthContextValue | null>(null);
const AUTH_INITIALIZATION_TIMEOUT_MS = 15_000;

type SessionRestorationResult = { ok: true; user: CurrentUser } | { ok: false; error: unknown };
interface SessionRestorationAttempt {
  generation: number;
  sessionId: string;
  promise: Promise<SessionRestorationResult>;
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error("Authentication verification timed out.")), timeoutMs);
    promise.then(
      (value) => { window.clearTimeout(timeout); resolve(value); },
      (error: unknown) => { window.clearTimeout(timeout); reject(error); },
    );
  });
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);
  const [sessionId, setSessionId] = React.useState<string | null>(getClientSessionId());
  const statusRef = React.useRef(status);
  React.useEffect(() => { statusRef.current = status; }, [status]);
  const [queryClient, setQueryClient] = React.useState(createSessionQueryClient);
  const queryClientRef = React.useRef(queryClient);
  const transitionId = React.useRef(0);
  const authorizationRefreshSession = React.useRef<string | null>(null);
  const restorationPromise = React.useRef<SessionRestorationAttempt | null>(null);
  const knownInvalidSession = React.useRef(false);

  const resetSessionCache = React.useCallback(() => {
    void queryClientRef.current.cancelQueries();
    queryClientRef.current.clear();
    const next = createSessionQueryClient();
    queryClientRef.current = next;
    setQueryClient(next);
  }, []);

  const initializeCurrentSession = React.useCallback(async (expectedTransition: number, expectedGeneration: number, expectedSessionId: string) => {
    // Strict Mode can invoke this twice. Share the request for this exact session,
    // but let each caller commit the result if it is still the current transition.
    let attempt = restorationPromise.current;
    if (!attempt || attempt.generation !== expectedGeneration || attempt.sessionId !== expectedSessionId) {
      const promise = withTimeout(api.getMe(), AUTH_INITIALIZATION_TIMEOUT_MS)
        .then((user): SessionRestorationResult => ({ ok: true, user }))
        .catch((error: unknown): SessionRestorationResult => ({ ok: false, error }));
      attempt = { generation: expectedGeneration, sessionId: expectedSessionId, promise };
      restorationPromise.current = attempt;
      const completedAttempt = attempt;
      void promise.then(() => {
        if (restorationPromise.current === completedAttempt) restorationPromise.current = null;
      });
    }

    const result = await attempt.promise;
    if (transitionId.current !== expectedTransition
      || getSessionGeneration() !== expectedGeneration
      || getClientSessionId() !== expectedSessionId) return;

    if (!result.ok) {
      // A current-session failure (401, unavailable API, or timeout) must end
      // initialization. clearTokens also aborts the outstanding authenticated request.
      clearTokens();
      setUser(null);
      setSessionId(null);
      setStatus("unauthenticated");
      knownInvalidSession.current = true;
      return;
    }

    setUser(result.user);
    setSessionId(expectedSessionId);
    setStatus("authenticated");
    knownInvalidSession.current = false;
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
      restorationPromise.current = null;
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
        if (statusRef.current === "authenticated" && hasSession() && !knownInvalidSession.current) {
          const expectedSessionId = getClientSessionId();
          const expectedGeneration = getSessionGeneration();
          void api.getMe().then((nextUser) => {
            if (statusRef.current === "authenticated"
              && getSessionGeneration() === expectedGeneration
              && getClientSessionId() === expectedSessionId) setUser(nextUser);
          }).catch((error) => {
            // A transient refresh failure leaves the current view intact; authenticated API
            // requests still resolve current permissions on the server.
            if (error instanceof ApiError && error.status === 401) knownInvalidSession.current = true;
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
    restorationPromise.current = null;
    clearTokens();
    knownInvalidSession.current = false;
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
      const currentUser = await withTimeout(api.getMe(), AUTH_INITIALIZATION_TIMEOUT_MS);
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
    restorationPromise.current = null;
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
      const activeSessionId = getClientSessionId();
      if (statusRef.current !== "authenticated" || !activeSessionId
        || authorizationRefreshSession.current === activeSessionId) return;
      // A 403 can be a stable endpoint permission or scope denial. Refresh the
      // authoritative user permissions at most once per local session so a
      // query remount/re-fetch cycle cannot become a permission-refresh loop.
      authorizationRefreshSession.current = activeSessionId;
      // Keep current UI state in place during refresh. Clearing permissions
      // here toggles permission-gated queries off and on, causing unrelated
      // query observers to remount and repeat their requests.
      void refreshUser()
        .catch(() => undefined);
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
