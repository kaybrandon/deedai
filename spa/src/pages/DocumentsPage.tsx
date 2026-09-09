import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type DocumentListItem, type UserSummary } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
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
  const [selected, setSelected] = useState<string[]>([]);
  const [bulkAssignee, setBulkAssignee] = useState("");
  const [pendingDelete, setPendingDelete] = useState<DocumentListItem | null>(null);
  const [pendingRestore, setPendingRestore] = useState<DocumentListItem | null>(null);
  const [pendingHard, setPendingHard] = useState<DocumentListItem | null>(null);
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
    setSelected([]);
  }

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    endpoints.users().then(setUsers).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function toggle(id: string) {
    setSelected((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  }

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
      {canEdit && selected.length > 0 && (
        <div className="bulk-bar">
          <span>{selected.length} selected</span>
          <select value={bulkAssignee} onChange={(e) => setBulkAssignee(e.target.value)} aria-label="Bulk assignee">
            <option value="">Assignee</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.displayName}
              </option>
            ))}
          </select>
          <button
            className="primary"
            type="button"
            onClick={async () => {
              await endpoints.bulkAssign(selected, bulkAssignee || null);
              setNotice("Assigned selected deeds.");
              await load();
            }}
          >
            Bulk assign
          </button>
        </div>
      )}
      {notice && <div className="success-banner">{notice}</div>}
      {rows.length === 0 ? (
        <EmptyState title="No documents match" body="Try another Client, status, or upload a PDF to get started." />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {canEdit && <th />}
                <th>Name</th>
                <th>Client</th>
                <th>Status</th>
                <th>Updated</th>
                <th>Assignee</th>
                <th>Flags</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className={row.isDeleted ? "deleted-row" : undefined}>
                  {canEdit && (
                    <td>
                      <input type="checkbox" checked={selected.includes(row.id)} onChange={() => toggle(row.id)} />
                    </td>
                  )}
                  <td>{row.name}</td>
                  <td>{row.client}</td>
                  <td>
                    <StatusChip status={row.status} />
                  </td>
                  <td>
                    {new Date(row.updatedAt).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
                  </td>
                  <td>
                    {canEdit ? (
                      <select
                        value={row.assigneeUserId ?? ""}
                        aria-label={`Assignee for ${row.name}`}
                        onChange={async (e) => {
                          await endpoints.assign(row.id, e.target.value || null);
                          await load();
                        }}
                      >
                        <option value="">Unassigned</option>
                        {users.map((user) => (
                          <option key={user.id} value={user.id}>
                            {user.displayName}
                          </option>
                        ))}
                      </select>
                    ) : (
                      row.assignee ?? "—"
                    )}
                  </td>
                  <td>
                    {row.flags.map((flag) => (
                      <span key={flag.id} className="flag-pill" style={{ background: flag.color }}>
                        {flag.name}
                      </span>
                    ))}
                    {row.flags.length === 0 && "—"}
                  </td>
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
                      <>
                        <button className="primary" type="button" onClick={() => setPendingRestore(row)}>
                          Restore
                        </button>
                        <button className="ghost" type="button" onClick={() => setPendingHard(row)}>
                          Hard delete
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pendingDelete && (
        <ConfirmSheet
          title="Delete this deed?"
          body="Soft-delete. You can restore from Admin later on Restore / Manage Documents."
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
      {pendingRestore && (
        <ConfirmSheet
          title={`Restore ${pendingRestore.name}?`}
          body="This deed will appear on Documents again for its Client."
          confirmLabel="Restore"
          danger={false}
          onCancel={() => setPendingRestore(null)}
          onConfirm={async () => {
            await endpoints.restore(pendingRestore.id);
            setPendingRestore(null);
            setNotice("Restored.");
            await load();
          }}
        />
      )}
      {pendingHard && (
        <ConfirmSheet
          title={`Permanently delete ${pendingHard.name}?`}
          body="Hard-delete cannot be undone. Soft-delete first if this deed is still active."
          confirmLabel="Hard delete"
          onCancel={() => setPendingHard(null)}
          onConfirm={async () => {
            await endpoints.hardDelete(pendingHard.id);
            setPendingHard(null);
            setNotice("Permanently deleted.");
            await load();
          }}
        />
      )}
    </section>
  );
}
