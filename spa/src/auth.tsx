import { createContext, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type Me, type Role } from "./api";

interface AuthState {
  token: string | null;
  me: Me | null;
  ready: boolean;
  login: (token: string, me: Me) => void;
  logout: () => void;
  canUpload: boolean;
  canEdit: boolean;
  canAdmin: boolean;
}

const AuthContext = createContext<AuthState | null>(null);
const LAST_ACTIVITY_KEY = "deedai.lastActivity";
const DRAFT_DIRTY_KEY = "deedai.draftDirty";
const IDLE_HAD_DRAFT_KEY = "deedai.idleHadDraft";
const DEFAULT_IDLE_MINUTES = 30;

export function markDraftDirty(dirty: boolean) {
  if (dirty) {
    sessionStorage.setItem(DRAFT_DIRTY_KEY, "1");
  } else {
    sessionStorage.removeItem(DRAFT_DIRTY_KEY);
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate();
  const [token, setToken] = useState<string | null>(() => sessionStorage.getItem("deedai.token"));
  const [me, setMe] = useState<Me | null>(null);
  const [ready, setReady] = useState(false);
  const [idleMinutes, setIdleMinutes] = useState(DEFAULT_IDLE_MINUTES);
  const idleMinutesRef = useRef(idleMinutes);
  idleMinutesRef.current = idleMinutes;

  useEffect(() => {
    if (!token) {
      setMe(null);
      setReady(true);
      return;
    }
    endpoints
      .me()
      .then(setMe)
      .catch(() => {
        sessionStorage.removeItem("deedai.token");
        setToken(null);
        setMe(null);
      })
      .finally(() => setReady(true));
    endpoints
      .session()
      .then((config) => setIdleMinutes(config.idleTimeoutMinutes || DEFAULT_IDLE_MINUTES))
      .catch(() => setIdleMinutes(DEFAULT_IDLE_MINUTES));
  }, [token]);

  useEffect(() => {
    if (!token) {
      return;
    }

    const touch = () => sessionStorage.setItem(LAST_ACTIVITY_KEY, String(Date.now()));
    if (!sessionStorage.getItem(LAST_ACTIVITY_KEY)) {
      touch();
    }

    const events: Array<keyof WindowEventMap> = ["pointerdown", "keydown", "click", "scroll"];
    events.forEach((name) => window.addEventListener(name, touch, { passive: true }));

    const timer = window.setInterval(() => {
      const last = Number(sessionStorage.getItem(LAST_ACTIVITY_KEY) ?? Date.now());
      const timeoutMs = Math.max(1, idleMinutesRef.current || DEFAULT_IDLE_MINUTES) * 60_000;
      if (Date.now() - last < timeoutMs) {
        return;
      }

      if (sessionStorage.getItem(DRAFT_DIRTY_KEY) === "1") {
        sessionStorage.setItem(IDLE_HAD_DRAFT_KEY, "1");
      }
      sessionStorage.removeItem("deedai.token");
      sessionStorage.removeItem(LAST_ACTIVITY_KEY);
      sessionStorage.removeItem(DRAFT_DIRTY_KEY);
      setToken(null);
      setMe(null);
      navigate("/login?reason=idle", { replace: true });
    }, 15_000);

    return () => {
      events.forEach((name) => window.removeEventListener(name, touch));
      window.clearInterval(timer);
    };
  }, [token, navigate]);

  const value = useMemo<AuthState>(() => {
    const role = me?.role;
    return {
      token,
      me,
      ready,
      login: (nextToken, nextMe) => {
        sessionStorage.setItem("deedai.token", nextToken);
        sessionStorage.setItem(LAST_ACTIVITY_KEY, String(Date.now()));
        sessionStorage.removeItem(IDLE_HAD_DRAFT_KEY);
        setToken(nextToken);
        setMe(nextMe);
      },
      logout: () => {
        sessionStorage.removeItem("deedai.token");
        sessionStorage.removeItem(LAST_ACTIVITY_KEY);
        sessionStorage.removeItem(DRAFT_DIRTY_KEY);
        setToken(null);
        setMe(null);
      },
      canUpload: role === "Admin" || role === "Editor" || role === "Uploader",
      canEdit: role === "Admin" || role === "Editor",
      canAdmin: role === "Admin"
    };
  }, [token, me, ready]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return ctx;
}

export function roleCan(role: Role | undefined, action: "upload" | "edit" | "admin") {
  if (!role) return false;
  if (action === "admin") return role === "Admin";
  if (action === "edit") return role === "Admin" || role === "Editor";
  return role === "Admin" || role === "Editor" || role === "Uploader";
}
