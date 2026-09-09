import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  DEED_FIELDS,
  endpoints,
  type ClientItem,
  type DeedTypeItem,
  type DocumentListItem,
  type SoftwareFieldMapItem,
  type SoftwareLookup,
  type SoftwareSettings,
  type SoftwareStatus
} from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";

export default function SoftwarePage() {
  const { canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();
  const [status, setStatus] = useState<SoftwareStatus | null>(null);
  const [settings, setSettings] = useState<SoftwareSettings | null>(null);
  const [maps, setMaps] = useState<SoftwareFieldMapItem[]>([]);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [deedTypes, setDeedTypes] = useState<DeedTypeItem[]>([]);
  const [lookupDoc, setLookupDoc] = useState("");
  const [pushDoc, setPushDoc] = useState("");
  const [parcelId, setParcelId] = useState("");
  const [lookup, setLookup] = useState<SoftwareLookup | null>(null);
  const [mapForm, setMapForm] = useState({
    deedField: "grantor",
    softwareField: "",
    softwareGroup: "",
    clientId: "",
    deedType: "",
    isActive: true,
    sortOrder: 10
  });
  const [pendingMap, setPendingMap] = useState<SoftwareFieldMapItem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function load() {
    const [nextStatus, nextMaps, nextDocs, nextClients, nextTypes] = await Promise.all([
      endpoints.softwareStatus(),
      endpoints.softwareFieldMaps(),
      endpoints.documents(""),
      endpoints.clients(),
      endpoints.deedTypes()
    ]);
    setStatus(nextStatus);
    setMaps(nextMaps);
    setDocs(nextDocs);
    setClients(nextClients);
    setDeedTypes(nextTypes);
    if (canAdmin) {
      setSettings(await endpoints.softwareSettings());
    }
  }

  useEffect(() => {
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load Software."));
  }, [canAdmin]);

  async function saveSettings(next: SoftwareSettings) {
    const saved = await endpoints.updateSoftwareSettings({
      pushEnabled: next.pushEnabled,
      defaultGroup: next.defaultGroup,
      fieldDefaultsJson: next.fieldDefaultsJson
    });
    setSettings(saved);
    setStatus(await endpoints.softwareStatus());
    setNotice("Software settings saved.");
  }

  return (
    <section className="page">
      <h1>Software</h1>
      <p className="muted">Lookup and push to the external Software system. Never called CAMA. Advanced deed-type maps stay in Settings.</p>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}

      <section className="panel">
        <h2>Connection</h2>
        {status ? (
          <dl className="lookup-dl">
            <dt>Mode</dt>
            <dd>{status.mode}</dd>
            <dt>Status</dt>
            <dd>{status.connected ? "Connected" : "Not connected"}</dd>
            <dt>Push</dt>
            <dd>{status.pushEnabled ? "Enabled" : "Disabled"}</dd>
            <dt>Last sync</dt>
            <dd>
              {status.lastSyncAt
                ? `${status.lastSyncStatus ?? "—"} · ${new Date(status.lastSyncAt).toLocaleString()}${
                    status.lastDocumentName ? ` · ${status.lastDocumentName}` : ""
                  }`
                : "None yet"}
            </dd>
            <dt>Last fail</dt>
            <dd>{status.lastFailReason ?? "—"}</dd>
          </dl>
        ) : (
          <p>Loading connection…</p>
        )}
        {canAdmin && settings && (
          <div className="form-grid" style={{ marginTop: 16 }}>
            <label className="remember">
              <input
                type="checkbox"
                checked={settings.pushEnabled}
                onChange={(e) => void saveSettings({ ...settings, pushEnabled: e.target.checked })}
              />
              Enable Software push
            </label>
            <label>
              Default Software group
              <input
                value={settings.defaultGroup ?? ""}
                onChange={(e) => setSettings({ ...settings, defaultGroup: e.target.value })}
                onBlur={() => settings && void saveSettings(settings)}
              />
            </label>
          </div>
        )}
      </section>

      <section className="panel">
        <h2>Lookup</h2>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            try {
              if (lookupDoc) {
                setLookup(await endpoints.softwareLookup(lookupDoc));
              } else {
                const params = new URLSearchParams();
                if (parcelId) params.set("parcelId", parcelId);
                setLookup(await endpoints.softwareLookupKeys(`?${params}`));
              }
              setError(null);
            } catch (err) {
              setLookup(null);
              setError(err instanceof Error ? err.message : "Software lookup failed.");
            }
          }}
        >
          <select value={lookupDoc} onChange={(e) => setLookupDoc(e.target.value)} aria-label="Deed for lookup">
            <option value="">Or look up by parcel</option>
            {docs.map((doc) => (
              <option key={doc.id} value={doc.id}>
                {doc.name}
              </option>
            ))}
          </select>
          <input placeholder="Parcel ID" value={parcelId} onChange={(e) => setParcelId(e.target.value)} />
          <button className="primary" type="submit">
            Lookup
          </button>
        </form>
        {lookup && (
          <dl className="lookup-dl">
            <dt>Parcel</dt>
            <dd>{lookup.parcelId}</dd>
            <dt>Owner</dt>
            <dd>{lookup.owner ?? "—"}</dd>
            <dt>Address</dt>
            <dd>{lookup.address ?? "—"}</dd>
            <dt>Record</dt>
            <dd>{lookup.softwareRecordId ?? "—"}</dd>
          </dl>
        )}
      </section>

      <section className="panel">
        <h2>Push</h2>
        {canEdit ? (
          <div className="inline-form">
            <select value={pushDoc} onChange={(e) => setPushDoc(e.target.value)} aria-label="Deed to push">
              <option value="">Select a deed</option>
              {docs.map((doc) => (
                <option key={doc.id} value={doc.id}>
                  {doc.name}
                </option>
              ))}
            </select>
            <button
              className="primary"
              type="button"
              disabled={!pushDoc}
              onClick={async () => {
                try {
                  const result = await endpoints.softwarePush(pushDoc);
                  setNotice(result.succeeded ? result.message : result.failReason ?? result.message);
                  if (!result.succeeded) setError(result.failReason ?? result.message);
                  else setError(null);
                  setStatus(await endpoints.softwareStatus());
                } catch (err) {
                  setError(err instanceof Error ? err.message : "Software push failed.");
                }
              }}
            >
              Push
            </button>
          </div>
        ) : (
          <div>
            <p className="muted">Your role can look up Software records but cannot push.</p>
            <button className="nav-disabled" type="button" onClick={() => navigate("/denied", { state: { action: "push to Software" } })}>
              Push
            </button>
          </div>
        )}
      </section>

      <section className="panel">
        <h2>Field map</h2>
        <p className="muted">Map deed fields to Software fields and groups. These mappings are used on push.</p>
        {maps.length === 0 ? (
          <EmptyState title="No field maps yet" body="Add a deed field → Software field map so push uses the right group." />
        ) : (
          <ul className="setting-list">
            {maps.map((map) => (
              <li key={map.id}>
                <span>
                  {map.deedField} → {map.softwareGroup ? `${map.softwareGroup}.` : ""}
                  {map.softwareField}
                  <span className="muted">
                    {map.clientName ? ` · ${map.clientName}` : ""}
                    {map.deedType ? ` · ${map.deedType}` : ""}
                    {map.isActive ? "" : " · inactive"}
                  </span>
                </span>
                {canAdmin && (
                  <button className="link" type="button" onClick={() => setPendingMap(map)}>
                    Remove
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
        {canAdmin && (
          <form
            className="inline-form"
            onSubmit={async (event: FormEvent) => {
              event.preventDefault();
              await endpoints.createSoftwareFieldMap({
                ...mapForm,
                clientId: mapForm.clientId || null,
                deedType: mapForm.deedType || null
              });
              setMapForm({ deedField: "grantor", softwareField: "", softwareGroup: "", clientId: "", deedType: "", isActive: true, sortOrder: 10 });
              setNotice("Field map saved.");
              await load();
            }}
          >
            <select value={mapForm.deedField} onChange={(e) => setMapForm({ ...mapForm, deedField: e.target.value })} aria-label="Deed field">
              {DEED_FIELDS.map((field) => (
                <option key={field}>{field}</option>
              ))}
            </select>
            <input
              placeholder="Software field"
              value={mapForm.softwareField}
              onChange={(e) => setMapForm({ ...mapForm, softwareField: e.target.value })}
              required
            />
            <input
              placeholder="Software group"
              value={mapForm.softwareGroup}
              onChange={(e) => setMapForm({ ...mapForm, softwareGroup: e.target.value })}
            />
            <select value={mapForm.clientId} onChange={(e) => setMapForm({ ...mapForm, clientId: e.target.value })} aria-label="Client scope">
              <option value="">All Clients</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.name}
                </option>
              ))}
            </select>
            <select value={mapForm.deedType} onChange={(e) => setMapForm({ ...mapForm, deedType: e.target.value })} aria-label="Deed type scope">
              <option value="">Any deed type</option>
              {deedTypes.map((item) => (
                <option key={item.id} value={item.deedType}>
                  {item.deedType}
                </option>
              ))}
            </select>
            <button className="primary" type="submit">
              Add map
            </button>
          </form>
        )}
      </section>

      {pendingMap && (
        <ConfirmSheet
          title={`Remove ${pendingMap.deedField} map?`}
          body="Push will stop using this deed field → Software field mapping."
          confirmLabel="Remove"
          onCancel={() => setPendingMap(null)}
          onConfirm={async () => {
            await endpoints.deleteSoftwareFieldMap(pendingMap.id);
            setPendingMap(null);
            setNotice("Field map removed.");
            await load();
          }}
        />
      )}
    </section>
  );
}
