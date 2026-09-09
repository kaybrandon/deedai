import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { endpoints, type ClientItem, type DocumentListItem, type UserSummary } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import OcrRibbon from "../components/OcrRibbon";
import StatusChip from "../components/StatusChip";
import {
  applyDocumentsTable,
  documentsApiQuery,
  hasActiveDocumentsTableState,
  hasDocumentsListFilters,
  hasDocumentsTableParams,
  nextDocumentsSort,
  parseDocumentsTableQuery,
  patchDocumentsTableQuery,
  readStoredDocumentsTableQuery,
  serializeDocumentsTableQuery,
  writeStoredDocumentsTableQuery,
  type DocumentsSortKey,
  type DocumentsTableQuery
} from "../documentsTable";
import { displayStatus } from "../reviewStatus";
import { ribbonStepForDocument } from "../theme";

const statuses = [
  { value: "Queued", label: "Queued" },
  { value: "Processing", label: "Processing" },
  { value: "Ready", label: "Ready" },
  { value: "Failed", label: "Failed" },
  { value: "NeedsReview", label: "Needs Review" }
];

export default function DocumentsPage() {
  const { canEdit, canAdmin } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseDocumentsTableQuery(searchParams), [searchParams]);
  const [searchDraft, setSearchDraft] = useState(query.search);
  const searchTyping = useRef(false);
  const queryRef = useRef(query);
  queryRef.current = query;
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [rows, setRows] = useState<DocumentListItem[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [bulkAssignee, setBulkAssignee] = useState("");
  const [pendingDelete, setPendingDelete] = useState<DocumentListItem | null>(null);
  const [pendingRestore, setPendingRestore] = useState<DocumentListItem | null>(null);
  const [pendingHard, setPendingHard] = useState<DocumentListItem | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const fetchKey = [
    query.status,
    query.clientId,
    query.assigneeUserId,
    query.from,
    query.to,
    query.includeDeleted,
    query.type
  ].join("|");

  function applyQuery(next: DocumentsTableQuery) {
    setSearchParams(serializeDocumentsTableQuery(next), { replace: true });
    writeStoredDocumentsTableQuery(next);
  }

  function patchQuery(partial: Partial<DocumentsTableQuery>) {
    applyQuery(patchDocumentsTableQuery(query, partial));
  }

  async function fetchRows(next: DocumentsTableQuery) {
    setRows(await endpoints.documents(documentsApiQuery(next)));
    setSelected([]);
  }

  async function reload() {
    await fetchRows(query);
  }

  useEffect(() => {
    if (hasDocumentsTableParams(searchParams)) {
      writeStoredDocumentsTableQuery(parseDocumentsTableQuery(searchParams));
      return;
    }
    const stored = readStoredDocumentsTableQuery();
    if (stored && hasActiveDocumentsTableState(stored)) {
      setSearchParams(serializeDocumentsTableQuery(stored), { replace: true });
      setSearchDraft(stored.search);
    }
    // Hydrate once from the URL or session so chart deep links win over a stale filter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!searchTyping.current) {
      setSearchDraft(query.search);
    }
  }, [query.search]);

  useEffect(() => {
    if (searchDraft === queryRef.current.search) {
      searchTyping.current = false;
      return;
    }
    const timer = window.setTimeout(() => {
      applyQuery(patchDocumentsTableQuery(queryRef.current, { search: searchDraft }));
      searchTyping.current = false;
    }, 200);
    return () => window.clearTimeout(timer);
  }, [searchDraft]);

  useEffect(() => {
    void fetchRows(query);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fetchKey]);

  useEffect(() => {
    endpoints.clients().then(setClients).catch(() => undefined);
    endpoints.users().then(setUsers).catch(() => undefined);
  }, []);

  const table = useMemo(
    () => applyDocumentsTable(rows, { ...query, search: searchDraft }),
    [rows, query, searchDraft]
  );
  const deedTypes = useMemo(
    () => [...new Set(rows.map((row) => row.deedType).filter((value): value is string => Boolean(value)))].sort(),
    [rows]
  );

  function toggle(id: string) {
    setSelected((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  }

  function cell(value: string | null | undefined) {
    return value?.trim() ? value : "—";
  }

  return (
    <section className="page has-ocr-ribbon">
      <OcrRibbon current={ribbonStepForDocument(query.status || undefined, query.status === "NeedsReview" ? "NeedsReview" : query.status || undefined)} />
      <header className="page-head">
        <div>
          <h1>Documents</h1>
          <p className="page-kicker">Search, assign, and open deeds for your Clients.</p>
        </div>
      </header>
      <div className="filter-row documents-filter-row">
        <label className="documents-search-field">
          Search
          <input
            className="search-field documents-search"
            type="search"
            placeholder="Search deeds"
            value={searchDraft}
            onChange={(e) => {
              searchTyping.current = true;
              setSearchDraft(e.target.value);
            }}
            aria-label="Search deeds"
          />
        </label>
        <label>
          From
          <input
            type="date"
            aria-label="From date"
            value={query.from}
            onChange={(e) => patchQuery({ from: e.target.value })}
          />
        </label>
        <label>
          To
          <input
            type="date"
            aria-label="To date"
            value={query.to}
            onChange={(e) => patchQuery({ to: e.target.value })}
          />
        </label>
        {canAdmin && (
          <label className="remember">
            <input
              type="checkbox"
              checked={query.includeDeleted}
              onChange={(e) => patchQuery({ includeDeleted: e.target.checked })}
            />
            Show Deleted
          </label>
        )}
      </div>
      {canAdmin && rows.some((row) => row.canRetry) && (
        <div className="bulk-bar">
          <span>Failed deeds can be requeued to processing.</span>
          <button
            className="primary"
            type="button"
            onClick={async () => {
              const result = await endpoints.requeueFailed();
              setNotice(result.message);
              await reload();
            }}
          >
            Retry Failed
          </button>
        </div>
      )}
      {canEdit && selected.length > 0 && (
        <div className="bulk-bar">
          <span>{selected.length} selected</span>
          <select value={bulkAssignee} onChange={(e) => setBulkAssignee(e.target.value)} aria-label="Bulk Assignee">
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
              await reload();
            }}
          >
            Bulk Assign
          </button>
        </div>
      )}
      {notice && <div className="success-banner">{notice}</div>}
      {rows.length === 0 && !hasDocumentsListFilters(query) ? (
        <EmptyState title="No Documents Yet" body="Upload a PDF to get started." />
      ) : table.total === 0 ? (
        <EmptyState title="No Documents Match" body="Try another Client, status, or upload a PDF to get started." />
      ) : (
        <>
          <div className="table-wrap documents-table-wrap">
            <table className="documents-table" data-table="documents" aria-label="Documents">
              <thead>
                <tr>
                  {canEdit && <th />}
                  <th>Name</th>
                  <SortFilterTh
                    label="Status"
                    sortKey="status"
                    query={query}
                    onSort={(key) => applyQuery(nextDocumentsSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Status"
                        value={query.status}
                        onChange={(e) => patchQuery({ status: e.target.value })}
                      >
                        <option value="">All Statuses</option>
                        {statuses.map((item) => (
                          <option key={item.value} value={item.value}>
                            {item.label}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh
                    label="Client"
                    sortKey="client"
                    query={query}
                    onSort={(key) => applyQuery(nextDocumentsSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Client"
                        value={query.clientId}
                        onChange={(e) => patchQuery({ clientId: e.target.value })}
                      >
                        <option value="">All Clients</option>
                        {clients.map((client) => (
                          <option key={client.id} value={client.id}>
                            {client.name}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh label="Volume" sortKey="volume" query={query} onSort={(key) => applyQuery(nextDocumentsSort(query, key))} />
                  <SortFilterTh label="Page" sortKey="page" query={query} onSort={(key) => applyQuery(nextDocumentsSort(query, key))} />
                  <SortFilterTh
                    label="Type"
                    sortKey="type"
                    query={query}
                    onSort={(key) => applyQuery(nextDocumentsSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Type"
                        value={query.type}
                        onChange={(e) => patchQuery({ type: e.target.value })}
                      >
                        <option value="">All Types</option>
                        {deedTypes.map((type) => (
                          <option key={type} value={type}>
                            {type}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh label="PID" sortKey="pid" query={query} onSort={(key) => applyQuery(nextDocumentsSort(query, key))} />
                  <SortFilterTh label="Doc #" sortKey="documentNumber" query={query} onSort={(key) => applyQuery(nextDocumentsSort(query, key))} />
                  <SortFilterTh
                    label="Assignee"
                    sortKey="assignee"
                    query={query}
                    onSort={(key) => applyQuery(nextDocumentsSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Assignee"
                        value={query.assigneeUserId}
                        onChange={(e) => patchQuery({ assigneeUserId: e.target.value })}
                      >
                        <option value="">All Assignees</option>
                        {users.map((user) => (
                          <option key={user.id} value={user.id}>
                            {user.displayName}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh label="Updated" sortKey="updated" query={query} onSort={(key) => applyQuery(nextDocumentsSort(query, key))} />
                  <th>Flags</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {table.rows.map((row) => (
                  <tr key={row.id} className={row.isDeleted ? "deleted-row" : undefined}>
                    {canEdit && (
                      <td>
                        <input type="checkbox" checked={selected.includes(row.id)} onChange={() => toggle(row.id)} />
                      </td>
                    )}
                    <td>{row.name}</td>
                    <td>
                      <StatusChip status={displayStatus(row)} title={row.errorMessage} />
                    </td>
                    <td>{row.client}</td>
                    <td>{cell(row.volume)}</td>
                    <td>{cell(row.page)}</td>
                    <td>{cell(row.deedType)}</td>
                    <td>{cell(row.pid)}</td>
                    <td>{cell(row.documentNumber)}</td>
                    <td>
                      {canEdit ? (
                        <select
                          value={row.assigneeUserId ?? ""}
                          aria-label={`Assignee for ${row.name}`}
                          onChange={async (e) => {
                            await endpoints.assign(row.id, e.target.value || null);
                            await reload();
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
                      {new Date(row.updatedAt).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
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
                            await reload();
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
                            Hard Delete
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="documents-table-meta">
            Showing {table.rows.length} of {table.total}
          </div>
          {table.totalPages > 1 && (
            <div className="documents-pager">
              <button className="ghost" type="button" disabled={table.page <= 1} onClick={() => patchQuery({ page: table.page - 1 })}>
                Previous
              </button>
              <span>
                Page {table.page} of {table.totalPages}
              </span>
              <button
                className="ghost"
                type="button"
                disabled={table.page >= table.totalPages}
                onClick={() => patchQuery({ page: table.page + 1 })}
              >
                Next
              </button>
            </div>
          )}
        </>
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
            await reload();
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
            await reload();
          }}
        />
      )}
      {pendingHard && (
        <ConfirmSheet
          title={`Permanently delete ${pendingHard.name}?`}
          body="Hard-delete cannot be undone. Soft-delete first if this deed is still active."
          confirmLabel="Hard Delete"
          onCancel={() => setPendingHard(null)}
          onConfirm={async () => {
            await endpoints.hardDelete(pendingHard.id);
            setPendingHard(null);
            setNotice("Permanently deleted.");
            await reload();
          }}
        />
      )}
    </section>
  );
}

function SortFilterTh({
  label,
  sortKey,
  query,
  onSort,
  filter
}: {
  label: string;
  sortKey: DocumentsSortKey;
  query: DocumentsTableQuery;
  onSort: (key: DocumentsSortKey) => void;
  filter?: ReactNode;
}) {
  const active = query.sort === sortKey;
  const ariaSort = active ? (query.dir === "asc" ? "ascending" : "descending") : "none";
  return (
    <th className="documents-th" aria-sort={ariaSort}>
      <div className="documents-th-line">
        <button type="button" className={`th-sort${active ? " is-active" : ""}`} onClick={() => onSort(sortKey)}>
          {label}
          <span className="th-sort-affordance" aria-hidden="true">
            {active ? (query.dir === "asc" ? "▲" : "▼") : "↕"}
          </span>
        </button>
        {filter}
      </div>
    </th>
  );
}
