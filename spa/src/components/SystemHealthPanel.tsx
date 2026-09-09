import { useEffect, useState } from "react";
import { endpoints } from "../api";
import { FieldHelp } from "./FieldHelp";

type Check = { status: string; reachable: boolean; mode: string };

function sanitize(value: string | undefined): string {
  if (!value) return "—";
  if (/connection|key=|secret|password|accountkey/i.test(value)) {
    return "redacted";
  }
  return value;
}

export default function SystemHealthPanel() {
  const [status, setStatus] = useState<string | null>(null);
  const [product, setProduct] = useState("Deed AI");
  const [checks, setChecks] = useState<{ sql: Check; storage: Check; queue: Check } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    setBusy(true);
    try {
      const detail = await endpoints.healthDetail();
      setStatus(sanitize(detail.status));
      setProduct(sanitize(detail.product));
      setChecks({
        sql: {
          status: sanitize(detail.checks.sql.status),
          reachable: detail.checks.sql.reachable,
          mode: sanitize(detail.checks.sql.mode)
        },
        storage: {
          status: sanitize(detail.checks.storage.status),
          reachable: detail.checks.storage.reachable,
          mode: sanitize(detail.checks.storage.mode)
        },
        queue: {
          status: sanitize(detail.checks.queue.status),
          reachable: detail.checks.queue.reachable,
          mode: sanitize(detail.checks.queue.mode)
        }
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
        Admin-only view of <code>GET /api/health/detail</code>. Shows SQL, storage, and queue reachability
        and mode — never connection strings or keys.
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
          <HealthCheck name="SQL" check={checks.sql} />
          <HealthCheck name="Storage" check={checks.storage} />
          <HealthCheck name="Queue" check={checks.queue} />
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

function HealthCheck({ name, check }: { name: string; check: Check }) {
  const tone = check.reachable ? check.status : "fail";
  return (
    <div className={`health-chip is-${tone}`}>
      <strong className="health-chip-name">{name}</strong>
      <span className={`health-status is-${check.status}`}>{sanitize(check.status)}</span>
      <span className="muted">{check.reachable ? "Reachable" : "Unreachable"}</span>
      <span className="health-chip-mode">{sanitize(check.mode)}</span>
    </div>
  );
}
