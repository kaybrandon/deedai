import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { endpoints, type ApiError } from "../api";
import PasswordField from "../components/PasswordField";
import { validatePassword } from "../password";

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const nextPasswordError = validatePassword(password, true);
    const nextConfirmError = password !== confirm ? "Passwords do not match." : null;
    setPasswordError(nextPasswordError);
    setConfirmError(nextConfirmError);
    if (nextPasswordError || nextConfirmError) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await endpoints.resetPassword(token, password);
      navigate("/login", { replace: true });
    } catch (err) {
      const apiError = err as ApiError;
      if (apiError.field === "password") {
        setPasswordError(apiError.message ?? "Reset failed.");
      } else {
        setError(apiError.message ?? "Reset failed.");
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="login-card" onSubmit={onSubmit}>
        <h1>Choose a New Password</h1>
        <p className="subtitle">Use the link from your email.</p>
        {!token && <div className="denied-box">This reset link is missing a token.</div>}
        <PasswordField
          id="reset-password"
          label="New Password"
          value={password}
          required
          error={passwordError}
          onChange={(value) => {
            setPassword(value);
            if (passwordError) {
              setPasswordError(null);
            }
          }}
        />
        <label>
          Confirm Password
          <input
            id="reset-confirm"
            type="password"
            value={confirm}
            onChange={(e) => {
              setConfirm(e.target.value);
              if (confirmError) {
                setConfirmError(null);
              }
            }}
            required
            autoComplete="new-password"
            aria-invalid={confirmError ? true : undefined}
            aria-describedby={confirmError ? "reset-confirm-error" : undefined}
          />
          {confirmError && (
            <span className="field-error" id="reset-confirm-error" role="alert">
              {confirmError}
            </span>
          )}
        </label>
        <button className="primary" type="submit" disabled={busy || !token}>
          {busy ? "Saving…" : "Update Password"}
        </button>
        <Link className="link-plain" to="/login">
          Back to Sign In
        </Link>
        {error && (
          <div className="denied-box" role="alert">
            {error}
          </div>
        )}
    </form>
  );
}
