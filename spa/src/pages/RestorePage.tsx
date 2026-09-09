import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type DocumentListItem } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import StatusChip from "../components/StatusChip";

type Pending =
  | { kind: "restore"; row: DocumentListItem }
  | { kind: "hard"; row: DocumentListItem }
  | { kind: "purge" };

export default function RestorePage() {
  const { canAdmin } = useAuth();
  const navigate = useNavigate();
  const [clientId, setClientId] = useState("");
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [rows, setRows] = useState<DocumentListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);

  async function load(event?: FormEvent) {
    event?.preventDefault();
    const params = new URLSearchParams();
    if (clientId) params.set("clientId", clientId);
    try {
      setRows(await endpoints.deletedDocuments(`?${params}`));
      setError(null);
    } catch (err) {
      setRows([]);
      setError(err instanceof Error ? err.message : "Could not load deleted deeds.");
    }
  }

  useEffect(() => {
    if (!canAdmin) {
      navigate("/denied", { state: { action: "restore or hard-delete deeds" } });
      return;
    }
    endpoints.clients().then(setClients).catch(() => undefined);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canAdmin, navigate]);

  async function confirm() {
    if (!pending) return;
    try {
      if (pending.kind === "restore") {
        await endpoints.restore(pending.row.id);
        setNotice(`Restored ${pending.row.name}.`);
      } else if (pending.kind === "hard") {
        await endpoints.hardDelete(pending.row.id);
        setNotice(`Permanently deleted ${pending.row.name}.`);
      } else {
        const params = new URLSearchParams();
        if (clientId) params.set("clientId", clientId);
        const result = await endpoints.purgeDeleted(`?${params}`);
        setNotice(result.message);
      }
      setPending(null);
      setError(null);
      await load();
    } catch (err) {
      setPending(null);
      setError(err instanceof Error ? err.message : "Action failed.");
    }
  }

  return (
    <section className="page">
      <div className="review-header">
        <h1>Restore / Manage Documents</h1>
        <button className="danger" type="button" onClick={() => setPending({ kind: "purge" })}>
          Purge deleted
        </button>
      </div>
      <p className="muted">
        Soft-deleted deeds stay Client-scoped. Restore puts a deed back on Documents. Hard-delete and purge are permanent.
      </p>
      <form className="filter-row wrap" onSubmit={load}>
        <select value={clientId} onChange={(e) => setClientId(e.target.value)} aria-label="Client">
          <option value="">All Clients</option>
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
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      {rows.length === 0 && !error ? (
        <EmptyState title="No deleted deeds" body="Nothing to restore for this Client. Soft-delete a deed from Documents first." />
      ) : rows.length > 0 ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Client</th>
                <th>Status</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className="deleted-row">
                  <td>{row.name}</td>
                  <td>{row.client}</td>
                  <td>
                    <StatusChip status={row.status} />
                  </td>
                  <td>{new Date(row.updatedAt).toLocaleDateString()}</td>
                  <td className="actions-cell">
                    <button className="primary" type="button" onClick={() => setPending({ kind: "restore", row })}>
                      Restore
                    </button>
                    <button className="ghost" type="button" onClick={() => setPending({ kind: "hard", row })}>
                      Hard delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {pending?.kind === "restore" && (
        <ConfirmSheet
          title={`Restore ${pending.row.name}?`}
          body="This deed will appear on Documents again for its Client."
          confirmLabel="Restore"
          danger={false}
          onCancel={() => setPending(null)}
          onConfirm={() => void confirm()}
        />
      )}
      {pending?.kind === "hard" && (
        <ConfirmSheet
          title={`Permanently delete ${pending.row.name}?`}
          body="Hard-delete cannot be undone. The PDF blob is removed."
          confirmLabel="Hard delete"
          onCancel={() => setPending(null)}
          onConfirm={() => void confirm()}
        />
      )}
      {pending?.kind === "purge" && (
        <ConfirmSheet
          title="Purge all deleted deeds?"
          body={
            clientId
              ? "Permanently delete every soft-deleted deed for the selected Client."
              : "Permanently delete every soft-deleted deed. This cannot be undone."
          }
          confirmLabel="Purge"
          onCancel={() => setPending(null)}
          onConfirm={() => void confirm()}
        />
      )}
    </section>
  );
}
