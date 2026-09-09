import { FormEvent, useEffect, useState } from "react";
import { endpoints, type ClientItem, type DashboardCounts } from "../api";
import EmptyState from "../components/EmptyState";

function defaultBounds() {
  return { from: "2024-08-01", to: toInput(new Date()) };
}

function toInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

export default function DashboardPage() {
  const initial = defaultBounds();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [clientId, setClientId] = useState("");
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [counts, setCounts] = useState<DashboardCounts | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load(event?: FormEvent) {
    event?.preventDefault();
    const params = new URLSearchParams();
    if (from) params.set("from", new Date(from).toISOString());
    if (to) params.set("to", new Date(`${to}T23:59:59`).toISOString());
    if (clientId) params.set("clientId", clientId);
    try {
      setCounts(await endpoints.counts(`?${params}`));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load dashboard.");
    }
  }

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const empty = counts !== null && counts.uploaded === 0;

  return (
    <section className="page">
      <h1>Dashboard</h1>
      <form className="filter-row" onSubmit={load}>
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
        <button className="primary" type="submit">
          Search
        </button>
      </form>
      {error && <div className="denied-box">{error}</div>}
      {empty ? (
        <EmptyState title="No deeds in this range" body="Upload a PDF or widen the dates to see dashboard counts." />
      ) : (
        <div className="cards">
          <CountCard label="Uploaded" value={counts?.uploaded ?? 0} />
          <CountCard label="Queued" value={counts?.queued ?? 0} />
          <CountCard label="Processing" value={counts?.processing ?? 0} />
          <CountCard label="Ready" value={counts?.ready ?? 0} />
          <CountCard label="Failed" value={counts?.failed ?? 0} danger />
        </div>
      )}
    </section>
  );
}

function CountCard({ label, value, danger }: { label: string; value: number; danger?: boolean }) {
  return (
    <article className={`count-card ${danger ? "count-danger" : ""}`}>
      <span>{label}</span>
      <strong className={danger ? "danger-text" : ""}>{value}</strong>
    </article>
  );
}
