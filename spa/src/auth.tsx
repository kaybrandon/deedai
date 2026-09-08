import { createContext, useContext, useEffect, useMemo, useState } from "react";
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

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => sessionStorage.getItem("deedai.token"));
  const [me, setMe] = useState<Me | null>(null);
  const [ready, setReady] = useState(false);

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
  }, [token]);

  const value = useMemo<AuthState>(() => {
    const role = me?.role;
    return {
      token,
      me,
      ready,
      login: (nextToken, nextMe) => {
        sessionStorage.setItem("deedai.token", nextToken);
        setToken(nextToken);
        setMe(nextMe);
      },
      logout: () => {
        sessionStorage.removeItem("deedai.token");
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
