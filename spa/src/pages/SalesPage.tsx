import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type SaleRow } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";

export default function SalesPage() {
  const { canEdit } = useAuth();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [clientId, setClientId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [rows, setRows] = useState<SaleRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  function query() {
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (clientId) params.set("clientId", clientId);
    if (from) params.set("from", new Date(from).toISOString());
    if (to) params.set("to", new Date(`${to}T23:59:59`).toISOString());
    return `?${params}`;
  }

  async function load(event?: FormEvent) {
    event?.preventDefault();
    try {
      setRows(await endpoints.sales(query()));
      setError(null);
    } catch (err) {
      setRows([]);
      setError(err instanceof Error ? err.message : "Could not load sales.");
    }
  }

  useEffect(() => {
    if (!canEdit) {
      navigate("/denied", { state: { action: "open the Sales tab" } });
      return;
    }
    endpoints.clients().then(setClients).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canEdit, navigate]);

  return (
    <section className="page">
      <h1>Sales</h1>
      <p className="muted">Consideration, parties, and parcel for deeds your role can edit. This is the Software Sales tab.</p>
      <form className="filter-row wrap" onSubmit={load}>
        <input className="grow" placeholder="Search name, grantor, parcel" value={search} onChange={(e) => setSearch(e.target.value)} />
        <label>
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
        <select value={clientId} onChange={(e) => setClientId(e.target.value)} aria-label="Client">
          <option value="">Client</option>
          {clients.map((client) => (
            <option key={client.id} value={client.id}>
              {client.name}
            </option>
          ))}
        </select>
        <button className="primary" type="submit">
          Search
        </button>
      </form>
      {error && <div className="denied-box">{error}</div>}
      {rows === null ? (
        <p>Loading…</p>
      ) : rows.length === 0 ? (
        <EmptyState title="No sales match" body="Adjust Client or dates, or review a deed and save consideration." />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Deed</th>
                <th>Client</th>
                <th>Grantor</th>
                <th>Grantee</th>
                <th>Date</th>
                <th>Consideration</th>
                <th>Parcel</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td>
                    <button className="link" type="button" onClick={() => navigate(`/documents/${row.id}`)}>
                      {row.name}
                    </button>
                  </td>
                  <td>{row.client}</td>
                  <td>{row.grantor ?? "—"}</td>
                  <td>{row.grantee ?? "—"}</td>
                  <td>{row.instrumentDate ?? "—"}</td>
                  <td>{row.consideration ?? "—"}</td>
                  <td>{row.parcelId ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
