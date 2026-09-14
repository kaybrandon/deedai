import { FormEvent, useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  endpoints,
  type ClientItem,
  type DashboardByUser,
  type DashboardCounts,
  type DashboardStatusMix,
  type DashboardVolume
} from "../api";
import { useAuth } from "../auth";
import { ByUserChart, StatusMixChart, VolumeChart } from "../components/DashboardCharts";
import { IconCheck, IconClock, IconCloud, IconDownload, IconInbox, IconWarning } from "../components/GisIcons";
import { documentsPath } from "../documentsPath";

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

function buildExportQuery(from: string, to: string, clientId: string) {
  const params = new URLSearchParams(buildQuery(from, to, clientId));
  if (from) params.set("fromDate", from);
  if (to) params.set("toDate", to);
  return `?${params}`;
}

function dashboardFileName(from: string, to: string) {
  const start = from || "all";
  const end = to || "all";
  return `deedai-dashboard-${start}-to-${end}.pdf`;
}

function formatRange(from: string, to: string) {
  if (!from && !to) return "the selected dates";
  const start = from ? new Date(`${from}T00:00:00`).toLocaleDateString(undefined, { month: "long", day: "numeric" }) : "";
  const end = to ? new Date(`${to}T00:00:00`).toLocaleDateString(undefined, { month: "long", day: "numeric", year: "numeric" }) : "";
  if (start && end) return `${start} – ${end}`;
  return start || end;
}

export default function DashboardPage() {
  const { me } = useAuth();
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
    const exportParams = buildExportQuery(from, to, clientId);
    const nextApplied = { from, to, clientId };
    setExporting(true);
    try {
      const ok = await loadWith(params, nextApplied);
      if (!ok) {
        return;
      }
      await endpoints.exportDashboard(exportParams, dashboardFileName(nextApplied.from, nextApplied.to));
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
  const printClient = appliedClient?.name ?? "All Clients";
  const busy = loading || exporting;
  const identity = me?.displayName || me?.email || "signed-in user";

  return (
    <section className="page dashboard-page">
      <header className="page-head dashboard-title-row">
        <div>
          <h1 className="page-title no-print">Dashboard</h1>
          <p className="page-kicker no-print">Counts and charts for Clients you can access.</p>
          <div className="dashboard-print-head print-only">
            <h1>{printTitle}</h1>
            <p>
              Dashboard · {printRange} · Client {printClient}
            </p>
            <p>Generated {new Date().toISOString()}</p>
          </div>
        </div>
      </header>
      <article className="ant-card compact-card no-print" data-chrome="presence">
        <div className="ant-card-head">
          <div className="ant-card-head-wrapper">
            <div className="ant-card-head-title">Who’s online</div>
            <div className="ant-card-extra">1 online</div>
          </div>
        </div>
        <div className="ant-card-body">
          <span className="presence-dot is-online" aria-hidden="true" />
          {identity}
        </div>
      </article>
      <form className="filter-toolbar filter-row wrap no-print" onSubmit={load}>
        <label className="filter-field">
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label className="filter-field">
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
        <label className="filter-field">
          Client
          <select value={clientId} onChange={(e) => setClientId(e.target.value)}>
            <option value="">All Clients</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.name}
              </option>
            ))}
          </select>
        </label>
        <div className="filter-actions dashboard-export-actions">
          <button className="ghost ant-btn ant-btn-sm" type="button" disabled={busy} onClick={() => void handlePrint()}>Print</button>
          <button className="ghost ant-btn ant-btn-sm" type="button" disabled={busy} onClick={() => void handleExport()}>
            <IconDownload />
            {exporting ? "Exporting…" : "Export PDF"}
          </button>
          <button className="primary ant-btn ant-btn-sm" type="submit" disabled={busy}>
            {loading ? "Loading…" : "Apply"}
          </button>
        </div>
      </form>
      {error && <div className="denied-box no-print">{error}</div>}
      <div className="dashboard-print-surface">
        <div className="cards kpi-row">
          <CountCard
            label="Uploaded"
            value={counts?.uploaded ?? 0}
            tone="blue"
            icon={<IconCloud />}
            to={documentsPath({ ...applied })}
          />
          <CountCard
            label="Queued"
            value={counts?.queued ?? 0}
            tone="gray"
            icon={<IconInbox />}
            to={documentsPath({ status: "Queued", ...applied })}
          />
          <CountCard
            label="Processing"
            value={counts?.processing ?? 0}
            tone="gold"
            icon={<IconClock />}
            to={documentsPath({ status: "Processing", ...applied })}
          />
          <CountCard
            label="Ready"
            value={counts?.ready ?? 0}
            tone="green"
            icon={<IconCheck />}
            to={documentsPath({ status: "Ready", ...applied })}
          />
          <CountCard
            label="Failed"
            value={counts?.failed ?? 0}
            tone="red"
            danger
            icon={<IconWarning />}
            to={documentsPath({ status: "Failed", ...applied })}
          />
        </div>
        <div className="chart-grid">
          <article className="ant-card chart-card compact-card">
            <div className="ant-card-head">
              <div className="ant-card-head-wrapper">
                <h2>Status Mix</h2>
              </div>
            </div>
            <div className="ant-card-body">
              <StatusMixChart data={mix} {...applied} />
            </div>
          </article>
          <article className="ant-card chart-card compact-card">
            <div className="ant-card-head">
              <div className="ant-card-head-wrapper">
                <h2>By Users</h2>
              </div>
            </div>
            <div className="ant-card-body">
              <ByUserChart data={byUser} {...applied} />
            </div>
          </article>
          <article className="ant-card chart-card chart-card-wide compact-card">
            <div className="ant-card-head">
              <div className="ant-card-head-wrapper">
                <h2>Volume Over Time</h2>
              </div>
            </div>
            <div className="ant-card-body">
              <VolumeChart data={volume} {...applied} />
            </div>
          </article>
        </div>
        <p className="visually-hidden">{`Work over ${formatRange(applied.from, applied.to)}`}</p>
      </div>
    </section>
  );
}

function CountCard({
  label,
  value,
  danger,
  tone,
  icon,
  to
}: {
  label: string;
  value: number;
  danger?: boolean;
  tone: "blue" | "gray" | "gold" | "green" | "red";
  icon: ReactNode;
  to: string;
}) {
  return (
    <Link
      to={to}
      className={`ant-card kpi-card count-card count-card-link ${danger ? "count-danger" : ""}`}
      role="button"
      tabIndex={0}
      aria-label={`View ${label} documents`}
    >
      <div className="ant-card-body">
        <div className="ant-statistic">
          <div className="ant-statistic-title">{label}</div>
          <div className="ant-statistic-content">
            <span className={`ant-statistic-content-prefix kpi-icon kpi-icon-${tone}`}>{icon}</span>
            <strong className={`ant-statistic-content-value${danger ? " danger-text" : ""}`}>{value}</strong>
          </div>
        </div>
      </div>
    </Link>
  );
}
