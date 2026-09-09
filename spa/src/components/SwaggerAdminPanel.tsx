import { useEffect, useState } from "react";
import { endpoints, sessionToken } from "../api";
import { FieldHelp } from "./FieldHelp";

function swaggerHref() {
  return "/swagger";
}

export default function SwaggerAdminPanel() {
  const [enabled, setEnabled] = useState(false);
  const [ready, setReady] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [copyMessage, setCopyMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!copyMessage) {
      return;
    }
    const id = window.setTimeout(() => setCopyMessage(null), 2000);
    return () => window.clearTimeout(id);
  }, [copyMessage]);

  useEffect(() => {
    let cancelled = false;
    void endpoints
      .swaggerSetting()
      .then((row) => {
        if (cancelled) return;
        setEnabled(row.enabled);
        setLoadError(null);
        setReady(true);
      })
      .catch((err) => {
        if (cancelled) return;
        setLoadError(err instanceof Error ? err.message : "Could not load Swagger setting.");
        setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function toggle() {
    setBusy(true);
    setSaveError(null);
    try {
      const row = await endpoints.saveSwaggerSetting(!enabled);
      setEnabled(row.enabled);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Could not update Swagger.");
    } finally {
      setBusy(false);
    }
  }

  async function copyToken() {
    const value = sessionToken();
    if (!value) {
      setCopyMessage("Sign in again, then copy the token.");
      return;
    }
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(value);
      } else {
        throw new Error("clipboard unavailable");
      }
      setCopyMessage("Copied");
    } catch {
      try {
        const field = document.createElement("textarea");
        field.value = value;
        field.setAttribute("readonly", "");
        field.style.position = "fixed";
        field.style.left = "-9999px";
        document.body.appendChild(field);
        field.select();
        const ok = document.execCommand("copy");
        document.body.removeChild(field);
        if (!ok) throw new Error("copy command failed");
        setCopyMessage("Copied");
      } catch {
        setCopyMessage("Could not copy. Check browser clipboard permission.");
      }
    }
  }

  return (
    <section className={`panel swagger-panel${enabled ? "" : " is-off"}`} id="swagger">
      <h2>
        API Documentation (Swagger) <FieldHelp helpKey="settings.swagger" />
      </h2>
      <p className="muted">
        Admins only. Stored in the database — no App Setting change needed. Off by default.
      </p>
      {loadError && <div className="denied-box">{loadError}</div>}
      <div className="swagger-controls">
        <button
          type="button"
          className={`setting-toggle${enabled ? " is-on" : ""}`}
          role="switch"
          aria-checked={enabled}
          aria-label="Enable Swagger UI"
          disabled={busy || !ready}
          onClick={() => void toggle()}
        >
          <span className="setting-toggle-track" aria-hidden="true">
            <span className="setting-toggle-thumb" />
          </span>
          <span>Enable Swagger UI</span>
          <span className={`swagger-badge${enabled ? " is-on" : " is-off"}`}>{enabled ? "On" : "Off"}</span>
        </button>
        <button type="button" className="ghost swagger-copy" onClick={() => void copyToken()}>
          Copy Bearer
        </button>
        {enabled && (
          <a className="primary swagger-open" href={swaggerHref()} target="_blank" rel="noreferrer">
            Open Swagger UI
          </a>
        )}
      </div>
      <p className="muted">
        When on, <code>/swagger</code> serves the UI. Authorize with the copied session JWT — this does not open
        anonymous API access. When off, <code>/swagger</code> returns 404.
      </p>
      {saveError && <div className="denied-box">{saveError}</div>}
      {copyMessage && (
        <p className="muted" data-testid="swagger-copy-feedback">
          {copyMessage}
        </p>
      )}
    </section>
  );
}
