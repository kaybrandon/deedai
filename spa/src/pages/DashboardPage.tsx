import { FormEvent, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  endpoints,
  type ClientItem,
  type DashboardByUser,
  type DashboardCounts,
  type DashboardStatusMix,
  type DashboardVolume
} from "../api";
import { ByUserChart, StatusMixChart, VolumeChart } from "../components/DashboardCharts";

function documentsPath(status: string | undefined, clientId: string) {
  const params = new URLSearchParams();
  if (status) params.set("status", status);
  if (clientId) params.set("clientId", clientId);
  const query = params.toString();
  return query ? `/documents?${query}` : "/documents";
}

function defaultBounds() {
  return { from: "2024-08-01", to: toInput(new Date()) };
}

function toInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function buildQuery(from: string, to: string, clientId: string) {
  const params = new URLSearchParams();
  if (from) params.set("from", new Date(from).toISOString());
  if (to) params.set("to", new Date(`${to}T23:59:59`).toISOString());
  if (clientId) params.set("clientId", clientId);
  return `?${params}`;
}

function dashboardFileName(from: string, to: string) {
  const start = from || "all";
  const end = to || "all";
  return `deedai-dashboard-${start}-to-${end}.pdf`;
}

export default function DashboardPage() {
  const initial = defaultBounds();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [clientId, setClientId] = useState("");
  const [applied, setApplied] = useState({ from: initial.from, to: initial.to, clientId: "" });
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [counts, setCounts] = useState<DashboardCounts | null>(null);
  const [mix, setMix] = useState<DashboardStatusMix | null>(null);
  const [byUser, setByUser] = useState<DashboardByUser | null>(null);
  const [volume, setVolume] = useState<DashboardVolume | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);

  function query() {
    return buildQuery(from, to, clientId);
  }

  async function loadWith(params: string, nextApplied: { from: string; to: string; clientId: string }) {
    setLoading(true);
    try {
      const [nextCounts, nextMix, nextByUser, nextVolume] = await Promise.all([
        endpoints.counts(params),
        endpoints.statusMix(params),
        endpoints.byUser(params),
        endpoints.volume(params)
      ]);
      setCounts(nextCounts);
      setMix(nextMix);
      setByUser(nextByUser);
      setVolume(nextVolume);
      setApplied(nextApplied);
      setError(null);
      return true;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load dashboard.");
      return false;
    } finally {
      setLoading(false);
    }
  }

  async function load(event?: FormEvent) {
    event?.preventDefault();
    await loadWith(query(), { from, to, clientId });
  }

  async function handlePrint() {
    const params = query();
    const nextApplied = { from, to, clientId };
    const ok = await loadWith(params, nextApplied);
    if (!ok) {
      return;
    }
    await new Promise<void>((resolve) => {
      requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
    });
    window.print();
  }

  async function handleExport() {
    const params = query();
    const nextApplied = { from, to, clientId };
    setExporting(true);
    try {
      const ok = await loadWith(params, nextApplied);
      if (!ok) {
        return;
      }
      await endpoints.exportDashboard(params, dashboardFileName(nextApplied.from, nextApplied.to));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not export the dashboard PDF.");
    } finally {
      setExporting(false);
    }
  }

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const appliedClient = clients.find((client) => client.id === applied.clientId);
  const printTitle = appliedClient ? `${appliedClient.name} Deed AI` : "Deed AI";
  const printRange = `${applied.from || "all dates"} to ${applied.to || "all dates"}`;
  const printClient = appliedClient?.name ?? "All clients";
  const busy = loading || exporting;

  return (
    <section className="page dashboard-page">
      <header className="page-head">
        <div>
          <h1 className="no-print">Dashboard</h1>
          <p className="page-kicker no-print">Counts and charts for Clients you can access.</p>
          <div className="dashboard-print-head print-only">
            <h1>{printTitle}</h1>
            <p>
              Dashboard · {printRange} · Client {printClient}
            </p>
            <p>Generated {new Date().toISOString()}</p>
          </div>
        </div>
        <div className="row-actions dashboard-export-actions no-print">
          <button className="ghost" type="button" disabled={busy} onClick={() => void handlePrint()}>Print</button>
          <button className="primary" type="button" disabled={busy} onClick={() => void handleExport()}>
            {exporting ? "Exporting…" : "Export PDF"}
          </button>
        </div>
      </header>
      <form className="filter-row wrap no-print" onSubmit={load}>
        <label>
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
        <label>
          Client
          <select value={clientId} onChange={(e) => setClientId(e.target.value)}>
            <option value="">All clients</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.name}
              </option>
            ))}
          </select>
        </label>
        <button className="primary" type="submit" disabled={busy}>
          {loading ? "Loading…" : "Apply"}
        </button>
      </form>
      {error && <div className="denied-box no-print">{error}</div>}
      <div className="dashboard-print-surface">
        <div className="cards">
          <CountCard label="Uploaded" value={counts?.uploaded ?? 0} to={documentsPath(undefined, applied.clientId)} />
          <CountCard label="Queued" value={counts?.queued ?? 0} to={documentsPath("Queued", applied.clientId)} />
          <CountCard label="Processing" value={counts?.processing ?? 0} to={documentsPath("Processing", applied.clientId)} />
          <CountCard label="Ready" value={counts?.ready ?? 0} to={documentsPath("Ready", applied.clientId)} />
          <CountCard label="Failed" value={counts?.failed ?? 0} danger to={documentsPath("Failed", applied.clientId)} />
        </div>
        <div className="chart-grid">
          <article className="chart-card">
            <h2>Status mix</h2>
            <StatusMixChart data={mix} />
          </article>
          <article className="chart-card">
            <h2>By user</h2>
            <ByUserChart data={byUser} />
          </article>
          <article className="chart-card chart-card-wide">
            <h2>Volume over time</h2>
            <VolumeChart data={volume} />
          </article>
        </div>
      </div>
    </section>
  );
}

function CountCard({
  label,
  value,
  danger,
  to
}: {
  label: string;
  value: number;
  danger?: boolean;
  to: string;
}) {
  return (
    <Link
      to={to}
      className={`count-card count-card-link ${danger ? "count-danger" : ""}`}
      aria-label={`View ${label} documents`}
    >
      <span>{label}</span>
      <strong className={danger ? "danger-text" : ""}>{value}</strong>
    </Link>
  );
}
