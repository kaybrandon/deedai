import { useEffect, useState } from "react";
import { endpoints, type HealthCheck } from "../api";
import { FieldHelp } from "./FieldHelp";

function sanitize(value: string | undefined | null): string {
  if (!value) return "—";
  if (/connection|key=|secret|password|accountkey/i.test(value)) {
    return "redacted";
  }
  return value;
}

export default function SystemHealthPanel() {
  const [status, setStatus] = useState<string | null>(null);
  const [product, setProduct] = useState("Deed AI");
  const [checks, setChecks] = useState<{ name: string; check: HealthCheck }[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    setBusy(true);
    try {
      const detail = await endpoints.healthDetail();
      setStatus(sanitize(detail.status));
      setProduct(sanitize(detail.product));
      const rows = [
        { name: "SQL", check: detail.checks.sql },
        { name: "Blob", check: detail.checks.blob ?? detail.checks.storage },
        { name: "OCR queue", check: detail.checks.ocrQueue ?? detail.checks.queue },
        { name: "Document Intelligence", check: detail.checks.documentIntelligence },
        { name: "OCR pipeline", check: detail.checks.ocrPipeline }
      ].filter((row): row is { name: string; check: HealthCheck } => Boolean(row.check));
      setChecks(
        rows.map((row) => ({
          name: row.name,
          check: {
            status: sanitize(row.check.status),
            reachable: row.check.reachable,
            mode: sanitize(row.check.mode),
            detail: row.check.detail ? sanitize(row.check.detail) : undefined
          }
        }))
      );
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load system health.");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  return (
    <section className="panel" id="system-health">
      <h2>
        System health <FieldHelp helpKey="settings.systemHealth" />
      </h2>
      <p className="muted">
        Admin-only view of <code>GET /api/health/detail</code>. SQL, Blob read/write, OCR queue, Document
        Intelligence, and OCR pipeline — never connection strings or keys.
      </p>
      {error && <div className="denied-box">{error}</div>}
      {status && (
        <p>
          Overall: <strong className={`health-status is-${status}`}>{status}</strong>
          <span className="muted"> · {product}</span>
        </p>
      )}
      {checks && (
        <div className="health-row">
          {checks.map((row) => (
            <HealthChip key={row.name} name={row.name} check={row.check} />
          ))}
        </div>
      )}
      <div className="row-actions">
        <button className="ghost" type="button" disabled={busy} onClick={() => void load()}>
          Refresh
        </button>
      </div>
    </section>
  );
}

function HealthChip({ name, check }: { name: string; check: HealthCheck }) {
  const tone = check.reachable ? check.status : "fail";
  return (
    <div className={`health-chip is-${tone}`}>
      <strong className="health-chip-name">{name}</strong>
      <span className={`health-status is-${check.status}`}>{sanitize(check.status)}</span>
      <span className="muted">{check.reachable ? "Reachable" : "Unreachable"}</span>
      <span className="health-chip-mode">{sanitize(check.mode)}</span>
      {check.detail && <span className="health-chip-detail">{sanitize(check.detail)}</span>}
    </div>
  );
}
