import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type SaleRow, type SalesPage, type SalesTabCodeItem } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";
import { FieldHelp, LabelWithHelp } from "../components/FieldHelp";

export default function SalesPage() {
  const { canEdit } = useAuth();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [clientId, setClientId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [page, setPage] = useState<SalesPage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

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
      setPage(await endpoints.sales(query()));
      setError(null);
    } catch (err) {
      setPage({ displaySalesTab: false, considerationThreshold: null, codes: [], rows: [] });
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

  async function assign(row: SaleRow, code: string) {
    try {
      const updated = await endpoints.assignSalesTabCode(row.id, code || null);
      setPage((current) =>
        current
          ? { ...current, rows: current.rows.map((item) => (item.id === updated.id ? { ...item, salesTabCode: updated.salesTabCode } : item)) }
          : current
      );
      setNotice(`Sales Tab code ${updated.salesTabCode ?? "cleared"} saved.`);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not assign Sales Tab code.");
    }
  }

  const codes: SalesTabCodeItem[] = page?.codes ?? [];

  return (
    <section className="page">
      <h1>
        Sales <FieldHelp helpKey="sales.codes" />
      </h1>
      <p className="muted">
        Sales Tab codes for deeds that meet the Client consideration threshold when Display Sales Tab is on. Admin and
        Editor only.
      </p>
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
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      {page === null ? (
        <p>Loading…</p>
      ) : !page.displaySalesTab ? (
        <EmptyState
          title="Sales Tab Is Off"
          body="An Admin can turn on Display Sales Tab and set a consideration threshold for a Client on the Software page."
        />
      ) : (
        <>
          <p className="muted">
            <LabelWithHelp helpKey="sales.considerationThreshold">Threshold</LabelWithHelp>{" "}
            {page.considerationThreshold ?? 0}. Codes:{" "}
            {codes.length === 0 ? "none yet" : codes.map((code) => `${code.code} (${code.label})`).join(", ")}
          </p>
          {page.rows.length === 0 ? (
            <EmptyState title="No Sales Match" body="Adjust Client or dates, or review a deed and save consideration at or above the threshold." />
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
                    <th>Sales Tab</th>
                    <th>Parcel</th>
                  </tr>
                </thead>
                <tbody>
                  {page.rows.map((row) => (
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
                      <td>
                        <select
                          aria-label={`Sales Tab code for ${row.name}`}
                          value={row.salesTabCode ?? ""}
                          onChange={(e) => void assign(row, e.target.value)}
                        >
                          <option value="">Assign Code</option>
                          {codes.map((code) => (
                            <option key={code.id} value={code.code}>
                              {code.code} — {code.label}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td>{row.parcelId ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}
