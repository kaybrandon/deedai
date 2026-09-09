import { FormEvent, useEffect, useState } from "react";
import { endpoints, type EmailSettings } from "../api";
import ConfirmSheet from "./ConfirmSheet";
import { FieldHelp } from "./FieldHelp";

function formatWhen(value: string | null): string {
  if (!value) return "Never";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

export default function AdminEmailPanel() {
  const [settings, setSettings] = useState<EmailSettings | null>(null);
  const [fromName, setFromName] = useState("Deed AI");
  const [fromAddress, setFromAddress] = useState("noreply@bisconsultants.com");
  const [testTo, setTestTo] = useState("");
  const [confirmTest, setConfirmTest] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ passed: boolean; message: string } | null>(null);

  async function load() {
    const next = await endpoints.emailSettings();
    setSettings(next);
    setFromName(next.fromName);
    setFromAddress(next.fromAddress);
  }

  useEffect(() => {
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load email settings."));
  }, []);

  async function save(partial: Partial<EmailSettings> & { mode?: string; verifyRequired?: boolean }) {
    if (!settings) return;
    setBusy(true);
    try {
      const next = await endpoints.saveEmailSettings({
        mode: partial.mode ?? settings.mode,
        fromName,
        fromAddress,
        verifyRequired: partial.verifyRequired ?? settings.verifyRequired
      });
      setSettings(next);
      setFromName(next.fromName);
      setFromAddress(next.fromAddress);
      setNotice("Email settings saved.");
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save email settings.");
    } finally {
      setBusy(false);
    }
  }

  async function sendTest() {
    setBusy(true);
    setConfirmTest(false);
    try {
      const result = await endpoints.testEmail(testTo);
      setTestResult(result);
      setNotice(result.message);
      setError(null);
      await load();
    } catch (err) {
      setTestResult({ passed: false, message: err instanceof Error ? err.message : "Fail — test send did not run." });
      setError(err instanceof Error ? err.message : "Test send failed.");
    } finally {
      setBusy(false);
    }
  }

  if (!settings) {
    return (
      <section className="panel" id="admin-email">
        <h2>
          Admin email <FieldHelp helpKey="settings.emailMode" />
        </h2>
        <p className="muted">Loading email settings…</p>
        {error && <div className="denied-box">{error}</div>}
      </section>
    );
  }

  return (
    <section className="panel" id="admin-email">
      <h2>
        Admin email <FieldHelp helpKey="settings.emailMode" />
      </h2>
      <p className="muted">
        Switch <strong>SendGrid</strong> or <strong>SMTP</strong>. Only the active mode sends. Secrets stay in Key Vault —
        this page never shows API keys, usernames, or passwords.
      </p>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}

      <div className="mode-switch" role="group" aria-label="Email mode">
        {(["SendGrid", "Smtp"] as const).map((mode) => (
          <button
            key={mode}
            type="button"
            className={settings.mode === mode ? "primary" : "ghost"}
            aria-pressed={settings.mode === mode}
            disabled={busy}
            onClick={() => void save({ mode })}
          >
            {mode === "Smtp" ? "SMTP" : "SendGrid"}
          </button>
        ))}
      </div>

      <div className="health-row" aria-label="Email status">
        <div className={`health-chip is-${settings.configured ? "ok" : "fail"}`}>
          <strong className="health-chip-name">Active</strong>
          <span>{settings.mode === "Smtp" ? "SMTP" : "SendGrid"}</span>
        </div>
        <div className={`health-chip is-${settings.configured ? "ok" : "fail"}`}>
          <strong className="health-chip-name">Configured</strong>
          <span>{settings.configured ? "Yes" : "No"}</span>
        </div>
        <div className="health-chip">
          <strong className="health-chip-name">Last success</strong>
          <span>{formatWhen(settings.lastSuccessAt)}</span>
        </div>
        <div className={`health-chip${settings.lastFailAt ? " is-fail" : ""}`}>
          <strong className="health-chip-name">Last fail</strong>
          <span>{settings.lastFailReason ? `${formatWhen(settings.lastFailAt)} · ${settings.lastFailReason}` : formatWhen(settings.lastFailAt)}</span>
        </div>
      </div>

      <div className="email-kv-grid">
        <div>
          <h3>SendGrid (Key Vault)</h3>
          <p>
            API key: <strong>{settings.sendGridConfigured ? "Configured" : "Not configured"}</strong>
            {settings.sendGridKeyLast4 ? ` · last 4 ${settings.sendGridKeyLast4}` : ""}
          </p>
        </div>
        <div>
          <h3>SMTP (Key Vault)</h3>
          <p>
            Host {settings.smtpHostConfigured ? "configured" : "not configured"}
            {settings.smtpHost ? ` (${settings.smtpHost})` : ""} · Port{" "}
            {settings.smtpPortConfigured ? settings.smtpPort : "not configured"} · TLS{" "}
            {settings.smtpTls == null ? "—" : settings.smtpTls ? "on" : "off"}
          </p>
          <p>
            Username: <strong>{settings.smtpUsernameConfigured ? "Configured" : "Not configured"}</strong>
            {" · "}
            Password: <strong>{settings.smtpPasswordConfigured ? "Configured" : "Not configured"}</strong>
            {settings.smtpTimeoutSeconds ? ` · timeout ${settings.smtpTimeoutSeconds}s` : ""}
          </p>
        </div>
      </div>

      <form
        className="inline-form"
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          void save({});
        }}
      >
        <input
          aria-label="From name"
          placeholder="From name"
          value={fromName}
          onChange={(e) => setFromName(e.target.value)}
        />
        <input
          type="email"
          aria-label="From address"
          placeholder="From address"
          value={fromAddress}
          onChange={(e) => setFromAddress(e.target.value)}
          required
        />
        <button className="primary" type="submit" disabled={busy}>
          Save from
        </button>
      </form>

      <label className="remember">
        <input
          type="checkbox"
          checked={settings.verifyRequired}
          disabled={busy}
          onChange={(e) => void save({ verifyRequired: e.target.checked })}
        />
        <span>
          Require email verification to sign in <FieldHelp helpKey="settings.verifyRequired" />
        </span>
      </label>

      <form
        className="inline-form"
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          setConfirmTest(true);
        }}
      >
        <input
          type="email"
          aria-label="Test send address"
          placeholder="Admin test address"
          value={testTo}
          onChange={(e) => setTestTo(e.target.value)}
          required
        />
        <button className="primary" type="submit" disabled={busy}>
          Test send
        </button>
        <FieldHelp helpKey="settings.emailTest" />
      </form>
      {testResult && (
        <div className={testResult.passed ? "success-banner" : "denied-box"} role="status">
          {testResult.passed ? "Pass" : "Fail"} — {testResult.message}
        </div>
      )}

      {confirmTest && (
        <ConfirmSheet
          title="Send a test email?"
          body={`Send one test message to ${testTo} using the active ${settings.mode === "Smtp" ? "SMTP" : "SendGrid"} mode.`}
          confirmLabel="Send test"
          danger={false}
          helpKey="settings.emailTest"
          onCancel={() => setConfirmTest(false)}
          onConfirm={() => void sendTest()}
        />
      )}
    </section>
  );
}
