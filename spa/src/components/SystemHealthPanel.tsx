import { useEffect, useState } from "react";
import { endpoints, type HealthCheck, type OcrQueueVisibility } from "../api";
import { FieldHelp } from "./FieldHelp";

function sanitize(value: string | undefined | null): string {
  if (!value) return "—";
  if (/connection|key=|secret|password|accountkey|defaultendpoints|cognitiveservices|blob\.core|endpoint/i.test(value)) {
    return "redacted";
  }
  return value;
}

function formatAge(seconds: number | null): string {
  if (seconds == null) return "—";
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  if (minutes < 60) return rest === 0 ? `${minutes}m` : `${minutes}m ${rest}s`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins === 0 ? `${hours}h` : `${hours}h ${mins}m`;
}

function formatStamp(value: string | null): string {
  if (!value) return "—";
  return sanitize(value);
}

export default function SystemHealthPanel() {
  const [status, setStatus] = useState<string | null>(null);
  const [product, setProduct] = useState("Deed AI");
  const [checks, setChecks] = useState<{ name: string; check: HealthCheck }[] | null>(null);
  const [queue, setQueue] = useState<OcrQueueVisibility | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    setBusy(true);
    try {
      const detail = await endpoints.healthDetail();
      setStatus(sanitize(detail.status));
      setProduct(sanitize(detail.product));
      const rows: { name: string; check: HealthCheck }[] = [
        { name: "SQL", check: detail.checks.sql },
        { name: "Storage", check: detail.checks.storage },
        { name: "Queue", check: detail.checks.queue },
        { name: "Blob", check: detail.checks.blob },
        { name: "Document Intelligence", check: detail.checks.documentIntelligence },
        { name: "OCR pipeline", check: detail.checks.ocrPipeline }
      ];
      setChecks(
        rows.map((row) => ({
          name: row.name,
          check: {
            status: sanitize(row.check.status),
            reachable: row.check.reachable,
            mode: sanitize(row.check.mode),
            detail: row.check.detail ? sanitize(row.check.detail) : undefined,
            configured: row.check.configured
          }
        }))
      );
      setQueue({
        depth: detail.ocrQueue.depth,
        oldestWaitingAgeSeconds: detail.ocrQueue.oldestWaitingAgeSeconds,
        poisonCount: detail.ocrQueue.poisonCount,
        failedCount: detail.ocrQueue.failedCount,
        lastDiSuccessAt: detail.ocrQueue.lastDiSuccessAt,
        lastDiFailAt: detail.ocrQueue.lastDiFailAt
      });
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
        Admin-only view of <code>GET /api/health/detail</code>. SQL, Storage, and Queue stay as reachability.
        Blob is a write/read/delete canary. Document Intelligence and OCR pipeline are separate. OCR queue
        metrics are visibility on the existing buffer — never connection strings or keys.
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
      {queue && (
        <div className="health-metrics" aria-label="OCR queue visibility">
          <div className="health-metric">
            <span className="health-metric-label">Queue depth</span>
            <strong>{queue.depth}</strong>
          </div>
          <div className="health-metric">
            <span className="health-metric-label">Oldest waiting</span>
            <strong>{formatAge(queue.oldestWaitingAgeSeconds)}</strong>
          </div>
          <div className="health-metric">
            <span className="health-metric-label">Poison / Failed</span>
            <strong>
              {queue.poisonCount} / {queue.failedCount}
            </strong>
          </div>
          <div className="health-metric">
            <span className="health-metric-label">Last DI success</span>
            <strong>{formatStamp(queue.lastDiSuccessAt)}</strong>
          </div>
          <div className="health-metric">
            <span className="health-metric-label">Last DI fail</span>
            <strong>{formatStamp(queue.lastDiFailAt)}</strong>
          </div>
        </div>
      )}
      <div className="row-actions">
        <button className="ghost health-refresh" type="button" disabled={busy} onClick={() => void load()}>
          Refresh
        </button>
      </div>
    </section>
  );
}

function HealthChip({ name, check }: { name: string; check: HealthCheck }) {
  const tone = check.reachable ? check.status : "fail";
  const configured =
    check.configured == null ? null : check.configured ? "Configured" : "Not configured";
  return (
    <div className={`health-chip is-${tone}`}>
      <strong className="health-chip-name">{name}</strong>
      <span className={`health-status is-${check.status}`}>{sanitize(check.status)}</span>
      <span className="muted">{check.reachable ? "Reachable" : "Unreachable"}</span>
      <span className="health-chip-mode">{sanitize(check.mode)}</span>
      {configured && <span className="muted">{configured}</span>}
      {check.detail && <span className="health-chip-detail">{sanitize(check.detail)}</span>}
    </div>
  );
}
