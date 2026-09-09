import { FormEvent, useState } from "react";
import { Link } from "react-router-dom";
import { endpoints, type ApiError } from "../api";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await endpoints.forgotPassword(email);
      setNotice(result.message);
    } catch (err) {
      setError((err as ApiError).message ?? "Could not send reset email.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="login-card" onSubmit={onSubmit}>
        <h1>Reset password</h1>
        <p className="subtitle">We will email a reset link if that account exists.</p>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoComplete="username"
          />
        </label>
        <button className="primary" type="submit" disabled={busy}>
          {busy ? "Sending…" : "Send reset link"}
        </button>
        <Link className="link-plain" to="/login">
          Back to sign in
        </Link>
        {notice && <div className="success-banner">{notice}</div>}
        {error && (
          <div className="denied-box" role="alert">
            {error}
          </div>
        )}
    </form>
  );
}
