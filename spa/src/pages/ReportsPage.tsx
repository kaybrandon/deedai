import { FormEvent, useEffect, useState } from "react";
import { endpoints, type ClientItem, type UserSummary } from "../api";
import EmptyState from "../components/EmptyState";

interface ReportRow {
  id: string;
  name: string;
  client: string;
  status: string;
  reviewStatus?: string | null;
  deedType?: string | null;
  assignee?: string | null;
  updatedAt: string;
  flags?: string[];
}

export default function ReportsPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [clientId, setClientId] = useState("");
  const [assignee, setAssignee] = useState("");
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [rows, setRows] = useState<ReportRow[]>([]);
  const [error, setError] = useState<string | null>(null);

  function query() {
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (status) params.set("status", status);
    if (clientId) params.set("clientId", clientId);
    if (assignee) params.set("assigneeUserId", assignee);
    return `?${params}`;
  }

  async function load(event?: FormEvent) {
    event?.preventDefault();
    try {
      const data = await endpoints.reports(query());
      setRows(data as unknown as ReportRow[]);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load report.");
    }
  }

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    endpoints.users().then(setUsers).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <section className="page">
      <div className="review-header">
        <h1>Reports</h1>
        <div className="row-actions">
          <button className="ghost" type="button" onClick={() => endpoints.exportReport(query(), "csv", "deedai-report.csv")}>
            Export CSV
          </button>
          <button className="primary" type="button" onClick={() => endpoints.exportReport(query(), "xlsx", "deedai-report.xlsx")}>
            Export Excel
          </button>
        </div>
      </div>
      <form className="filter-row wrap" onSubmit={load}>
        <input className="grow" placeholder="Search name, grantor, parcel" value={search} onChange={(e) => setSearch(e.target.value)} />
        <select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Status">
          <option value="">Status</option>
          <option>Queued</option>
          <option>Processing</option>
          <option>Ready</option>
          <option>Failed</option>
        </select>
        <select value={clientId} onChange={(e) => setClientId(e.target.value)} aria-label="Client">
          <option value="">Client</option>
          {clients.map((client) => (
            <option key={client.id} value={client.id}>
              {client.name}
            </option>
          ))}
        </select>
        <select value={assignee} onChange={(e) => setAssignee(e.target.value)} aria-label="Assignee">
          <option value="">Assignee</option>
          {users.map((user) => (
            <option key={user.id} value={user.id}>
              {user.displayName}
            </option>
          ))}
        </select>
        <button className="primary" type="submit">
          Search
        </button>
      </form>
      {error && <div className="denied-box">{error}</div>}
      {rows.length === 0 ? (
        <EmptyState title="No rows for this report" body="Adjust filters or upload deeds, then export CSV or Excel." />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Client</th>
                <th>Status</th>
                <th>Deed type</th>
                <th>Assignee</th>
                <th>Flags</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td>{row.name}</td>
                  <td>{row.client}</td>
                  <td>{row.status}</td>
                  <td>{row.deedType ?? "—"}</td>
                  <td>{row.assignee ?? "—"}</td>
                  <td>{row.flags?.join(", ") || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
