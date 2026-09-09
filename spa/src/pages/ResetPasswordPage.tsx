import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { endpoints, type ApiError } from "../api";

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (password !== confirm) {
      setError("Passwords do not match.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await endpoints.resetPassword(token, password);
      navigate("/login", { replace: true });
    } catch (err) {
      setError((err as ApiError).message ?? "Reset failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={onSubmit}>
        <h1>Choose a new password</h1>
        <p className="subtitle">Use the link from your email.</p>
        {!token && <div className="denied-box">This reset link is missing a token.</div>}
        <label>
          New password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            autoComplete="new-password"
          />
        </label>
        <label>
          Confirm password
          <input
            type="password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            required
            minLength={8}
            autoComplete="new-password"
          />
        </label>
        <button className="primary" type="submit" disabled={busy || !token}>
          {busy ? "Saving…" : "Update password"}
        </button>
        <Link className="link-plain" to="/login">
          Back to sign in
        </Link>
        {error && (
          <div className="denied-box" role="alert">
            {error}
          </div>
        )}
      </form>
    </div>
  );
}
