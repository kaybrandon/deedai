import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { endpoints, type ApiError } from "../api";

export default function VerifyEmailPage() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!token) {
      setError("This verification link is missing a token.");
      return;
    }
    setBusy(true);
    endpoints
      .verifyEmail(token)
      .then((result) => {
        setNotice(result.message);
        setError(null);
      })
      .catch((err) => {
        setError((err as ApiError).message ?? "Verification failed.");
      })
      .finally(() => setBusy(false));
  }, [token]);

  return (
    <form className="login-card" onSubmit={(event) => event.preventDefault()}>
      <h1>Verify email</h1>
      <p className="subtitle">Confirm the address for your Deed AI account.</p>
      {busy && <p className="muted">Verifying…</p>}
      {notice && <div className="success-banner">{notice}</div>}
      {error && (
        <div className="denied-box" role="alert">
          {error}
        </div>
      )}
      <Link className="link-plain" to="/login">
        Back to sign in
      </Link>
    </form>
  );
}
