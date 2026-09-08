import { FormEvent, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ApiError } from "../api";
import { useAuth } from "../auth";

const EMAIL_KEY = "deedai.rememberedEmail";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState(() => localStorage.getItem(EMAIL_KEY) ?? "");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(() => Boolean(localStorage.getItem(EMAIL_KEY)));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [forgot, setForgot] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await endpoints.login(email, password);
      if (remember) {
        localStorage.setItem(EMAIL_KEY, email);
      } else {
        localStorage.removeItem(EMAIL_KEY);
      }
      login(result.token, {
        id: "",
        email: result.email,
        displayName: result.displayName,
        role: result.role
      });
      navigate("/dashboard");
    } catch (err) {
      const apiError = err as ApiError;
      setError(apiError.message ?? "Sign in failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={onSubmit}>
        <h1>Deed AI</h1>
        <p className="subtitle">Sign in to continue</p>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@company.com"
            required
            autoComplete="username"
          />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoComplete="current-password"
          />
        </label>
        <button className="primary" type="submit" disabled={busy}>
          {busy ? "Signing in…" : "Sign in"}
        </button>
        <div className="login-meta">
          <button className="link" type="button" onClick={() => setForgot(true)}>
            Forgot password?
          </button>
          <label className="remember">
            <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
            Remember email
          </label>
        </div>
        {error && (
          <div className="denied-box" role="alert">
            {error}
          </div>
        )}
        {forgot && (
          <div className="note-box">
            Contact your Admin to reset your password. Access denied shows a clear message (not a blank page).
          </div>
        )}
      </form>
    </div>
  );
}
