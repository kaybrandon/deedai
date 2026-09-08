import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type DocumentListItem, type UserSummary } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import StatusChip from "../components/StatusChip";

export default function DocumentsPage() {
  const { canEdit, canAdmin } = useAuth();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [clientId, setClientId] = useState("");
  const [assignee, setAssignee] = useState("");
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [rows, setRows] = useState<DocumentListItem[]>([]);
  const [pendingDelete, setPendingDelete] = useState<DocumentListItem | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function load(event?: FormEvent) {
    event?.preventDefault();
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (status) params.set("status", status);
    if (clientId) params.set("clientId", clientId);
    if (assignee) params.set("assigneeUserId", assignee);
    if (includeDeleted && canAdmin) params.set("includeDeleted", "true");
    setRows(await endpoints.documents(`?${params}`));
  }

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    endpoints.users().then(setUsers).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <section className="page">
      <h1>Documents</h1>
      <form className="filter-row wrap" onSubmit={load}>
        <input
          className="grow"
          placeholder="Search deeds"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
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
        {canAdmin && (
          <label className="remember">
            <input
              type="checkbox"
              checked={includeDeleted}
              onChange={(e) => setIncludeDeleted(e.target.checked)}
            />
            Show deleted
          </label>
        )}
        <button className="primary" type="submit">
          Search
        </button>
      </form>
      {notice && <div className="success-banner">{notice}</div>}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Client</th>
              <th>Status</th>
              <th>Updated</th>
              <th>Assignee</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id} className={row.isDeleted ? "deleted-row" : undefined}>
                <td>{row.name}</td>
                <td>{row.client}</td>
                <td>
                  <StatusChip status={row.status} />
                </td>
                <td>
                  {new Date(row.updatedAt).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
                </td>
                <td>{row.assignee ?? "—"}</td>
                <td className="actions-cell">
                  <button className="ghost" type="button" onClick={() => navigate(`/documents/${row.id}`)}>
                    Open
                  </button>
                  {row.canRetry && canEdit && (
                    <button
                      className="primary"
                      type="button"
                      onClick={async () => {
                        await endpoints.retry(row.id);
                        setNotice("Queued for OCR");
                        await load();
                      }}
                    >
                      Retry
                    </button>
                  )}
                  {canEdit && !row.isDeleted && (
                    <button className="ghost" type="button" onClick={() => setPendingDelete(row)}>
                      Delete
                    </button>
                  )}
                  {canAdmin && row.isDeleted && (
                    <button
                      className="primary"
                      type="button"
                      onClick={async () => {
                        await endpoints.restore(row.id);
                        setNotice("Restored.");
                        await load();
                      }}
                    >
                      Restore
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {pendingDelete && (
        <ConfirmSheet
          title="Delete this deed?"
          body="Soft-delete. You can restore from Admin later."
          confirmLabel="Delete"
          onCancel={() => setPendingDelete(null)}
          onConfirm={async () => {
            await endpoints.remove(pendingDelete.id);
            setPendingDelete(null);
            setNotice("Soft-deleted.");
            await load();
          }}
        />
      )}
    </section>
  );
}
