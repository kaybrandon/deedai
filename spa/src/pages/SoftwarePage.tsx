import { FormEvent, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  DEED_FIELDS,
  endpoints,
  type ClientItem,
  type DeedTypeItem,
  type DocumentListItem,
  type SalesTabCodeItem,
  type SoftwareClientConfig,
  type SoftwareFieldMapItem,
  type SoftwareLookup,
  type SoftwareSettings,
  type SoftwareStatus
} from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import { FieldHelp, LabelWithHelp } from "../components/FieldHelp";

const RESET_FLAGS: { key: keyof SoftwareClientConfig; label: string }[] = [
  { key: "resetExemptions", label: "Exemptions" },
  { key: "resetSupplementYear", label: "Supplement Year" },
  { key: "resetSalesLetter", label: "Sales Letter" },
  { key: "resetSalesTab", label: "Sales Tab" },
  { key: "resetAgents", label: "Agents" },
  { key: "resetMortgageCodes", label: "Mortgage Codes" }
];

export default function SoftwarePage() {
  const { canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();
  const [status, setStatus] = useState<SoftwareStatus | null>(null);
  const [settings, setSettings] = useState<SoftwareSettings | null>(null);
  const [configs, setConfigs] = useState<SoftwareClientConfig[]>([]);
  const [codes, setCodes] = useState<SalesTabCodeItem[]>([]);
  const [maps, setMaps] = useState<SoftwareFieldMapItem[]>([]);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [deedTypes, setDeedTypes] = useState<DeedTypeItem[]>([]);
  const [lookupDoc, setLookupDoc] = useState("");
  const [pushDoc, setPushDoc] = useState("");
  const [parcelId, setParcelId] = useState("");
  const [lookup, setLookup] = useState<SoftwareLookup | null>(null);
  const [configClientId, setConfigClientId] = useState("");
  const [draft, setDraft] = useState<SoftwareClientConfig | null>(null);
  const [mapForm, setMapForm] = useState({
    deedField: "grantor",
    softwareField: "",
    softwareGroup: "",
    clientId: "",
    deedType: "",
    isActive: true,
    sortOrder: 10
  });
  const [codeForm, setCodeForm] = useState({
    code: "",
    label: "",
    minConsideration: "1",
    maxConsideration: "",
    clientId: "",
    isActive: true,
    sortOrder: 10
  });
  const [pendingMap, setPendingMap] = useState<SoftwareFieldMapItem | null>(null);
  const [pendingCode, setPendingCode] = useState<SalesTabCodeItem | null>(null);
  const [pendingPush, setPendingPush] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedConfig = useMemo(
    () => configs.find((item) => item.clientId === configClientId) ?? null,
    [configs, configClientId]
  );

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
    if (canEdit) {
      const nextConfigs = await endpoints.softwareClientConfigs();
      setConfigs(nextConfigs);
      setConfigClientId((current) => current || nextConfigs[0]?.clientId || "");
      setCodes(await endpoints.salesTabCodes());
    }
    if (canAdmin) {
      setSettings(await endpoints.softwareSettings());
    }
  }

  useEffect(() => {
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load Software."));
  }, [canAdmin, canEdit]);

  useEffect(() => {
    if (selectedConfig) {
      setDraft(selectedConfig);
    }
  }, [selectedConfig]);

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

  async function saveClientConfig(next: SoftwareClientConfig) {
    const saved = await endpoints.updateSoftwareClientConfig(next.clientId, {
      vendor: next.vendor,
      apiUrl: next.apiUrl,
      groupCode: next.groupCode,
      removeLeadingZeros: next.removeLeadingZeros,
      dateLabelDepth: Number(next.dateLabelDepth),
      displaySalesTab: next.displaySalesTab,
      sendConsideration: next.sendConsideration,
      considerationThreshold: Number(next.considerationThreshold),
      resetExemptions: next.resetExemptions,
      resetSupplementYear: next.resetSupplementYear,
      resetSalesLetter: next.resetSalesLetter,
      resetSalesTab: next.resetSalesTab,
      resetAgents: next.resetAgents,
      resetMortgageCodes: next.resetMortgageCodes
    });
    setConfigs((current) => current.map((item) => (item.clientId === saved.clientId ? saved : item)));
    setDraft(saved);
    setNotice(`Software settings saved for ${saved.clientName}.`);
  }

  function resetLabels(config: SoftwareClientConfig) {
    return RESET_FLAGS.filter((flag) => config[flag.key] === true).map((flag) => flag.label);
  }

  function configForDoc(id: string) {
    const doc = docs.find((item) => item.id === id);
    return configs.find((item) => item.clientId === doc?.clientId) ?? null;
  }

  async function runPush(id: string) {
    const result = await endpoints.softwarePush(id);
    setNotice(result.succeeded ? result.message : result.failReason ?? result.message);
    if (!result.succeeded) setError(result.failReason ?? result.message);
    else setError(null);
    setStatus(await endpoints.softwareStatus());
  }

  return (
    <section className="page">
      <h1>Software</h1>
      <p className="muted">
        Lookup and push to the external Software system. Typed Client settings, property resets, and Sales Tab codes live
        here. Advanced deed-type maps stay in Settings.
      </p>
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
            <dt>API key</dt>
            <dd>{status.keyConfigured ? "Configured" : "Not configured"}</dd>
            <dt>Connection URL</dt>
            <dd>{status.connectionUrl ?? "—"}</dd>
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
              <LabelWithHelp helpKey="software.enablePush">Enable Software push</LabelWithHelp>
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

      {canAdmin && draft && (
        <section className="panel">
          <h2>Client Software settings</h2>
          <p className="muted">
            Vendor, API URL, group code, Sales Tab, and the six property resets are stored per Client. The API key stays
            in Key Vault / App Settings and is never shown here.
          </p>
          <label>
            Client
            <select
              value={configClientId}
              onChange={(e) => setConfigClientId(e.target.value)}
              aria-label="Client Software settings"
            >
              {configs.map((item) => (
                <option key={item.clientId} value={item.clientId}>
                  {item.clientName}
                </option>
              ))}
            </select>
          </label>
          <form
            className="form-grid"
            style={{ marginTop: 12 }}
            onSubmit={(event: FormEvent) => {
              event.preventDefault();
              void saveClientConfig(draft);
            }}
          >
            <label>
              <LabelWithHelp helpKey="software.vendor">Vendor</LabelWithHelp>
              <input value={draft.vendor ?? ""} onChange={(e) => setDraft({ ...draft, vendor: e.target.value })} />
            </label>
            <label>
              API URL
              <input value={draft.apiUrl ?? ""} onChange={(e) => setDraft({ ...draft, apiUrl: e.target.value })} />
            </label>
            <label>
              <LabelWithHelp helpKey="software.groupCode">Group code</LabelWithHelp>
              <input value={draft.groupCode ?? ""} onChange={(e) => setDraft({ ...draft, groupCode: e.target.value })} />
            </label>
            <label>
              <LabelWithHelp helpKey="software.dateLabelDepth">Mapped date/label depth</LabelWithHelp>
              <select
                value={draft.dateLabelDepth}
                onChange={(e) => setDraft({ ...draft, dateLabelDepth: Number(e.target.value) })}
                aria-label="Mapped date/label depth"
              >
                <option value={1}>1 — instrument date</option>
                <option value={2}>2 — instrument + updated</option>
                <option value={3}>3 — instrument + updated + created</option>
              </select>
            </label>
            <label>
              <LabelWithHelp helpKey="sales.considerationThreshold">Consideration threshold</LabelWithHelp>
              <input
                type="number"
                min={0}
                step="0.01"
                value={draft.considerationThreshold}
                onChange={(e) => setDraft({ ...draft, considerationThreshold: Number(e.target.value) })}
              />
            </label>
            <label className="remember">
              <input
                type="checkbox"
                checked={draft.removeLeadingZeros}
                onChange={(e) => setDraft({ ...draft, removeLeadingZeros: e.target.checked })}
              />
              <LabelWithHelp helpKey="software.removeLeadingZeros">Remove leading zeros</LabelWithHelp>
            </label>
            <label className="remember">
              <input
                type="checkbox"
                checked={draft.displaySalesTab}
                onChange={(e) => setDraft({ ...draft, displaySalesTab: e.target.checked })}
              />
              <LabelWithHelp helpKey="software.displaySalesTab">Display Sales Tab</LabelWithHelp>
            </label>
            <label className="remember">
              <input
                type="checkbox"
                checked={draft.sendConsideration}
                onChange={(e) => setDraft({ ...draft, sendConsideration: e.target.checked })}
              />
              <LabelWithHelp helpKey="software.sendConsideration">Send consideration</LabelWithHelp>
            </label>
            <div style={{ gridColumn: "1 / -1" }}>
              <h3>Property resets on push</h3>
              <p className="muted">When set, push clears that Software property group. Confirm before a destructive push.</p>
              <div className="form-grid">
                {RESET_FLAGS.map((flag) => (
                  <label key={flag.key} className="remember">
                    <input
                      type="checkbox"
                      checked={Boolean(draft[flag.key])}
                      onChange={(e) => setDraft({ ...draft, [flag.key]: e.target.checked })}
                    />
                    Reset {flag.label}
                  </label>
                ))}
              </div>
            </div>
            <button className="primary" type="submit">
              Save Client settings
            </button>
          </form>
        </section>
      )}

      {canAdmin && (
        <section className="panel">
          <h2>
            Sales Tab codes <FieldHelp helpKey="sales.codes" />
          </h2>
          <p className="muted">
            Codes assigned when Display Sales Tab is on and consideration meets the Client threshold. Editors assign them
            on the Sales page.
          </p>
          {codes.length === 0 ? (
            <EmptyState title="No Sales Tab codes" body="Add a code and consideration range used on push and the Sales page." />
          ) : (
            <ul className="setting-list">
              {codes.map((code) => (
                <li key={code.id}>
                  <span>
                    {code.code} — {code.label}
                    <span className="muted">
                      {` · ${code.minConsideration}${code.maxConsideration == null ? "+" : `–${code.maxConsideration}`}`}
                      {code.clientName ? ` · ${code.clientName}` : " · all Clients"}
                      {code.isActive ? "" : " · inactive"}
                    </span>
                  </span>
                  <button className="link" type="button" onClick={() => setPendingCode(code)}>
                    Remove
                  </button>
                </li>
              ))}
            </ul>
          )}
          <form
            className="inline-form"
            onSubmit={async (event: FormEvent) => {
              event.preventDefault();
              await endpoints.createSalesTabCode({
                code: codeForm.code,
                label: codeForm.label,
                minConsideration: Number(codeForm.minConsideration),
                maxConsideration: codeForm.maxConsideration === "" ? null : Number(codeForm.maxConsideration),
                clientId: codeForm.clientId || null,
                isActive: codeForm.isActive,
                sortOrder: codeForm.sortOrder
              });
              setCodeForm({ code: "", label: "", minConsideration: "1", maxConsideration: "", clientId: "", isActive: true, sortOrder: 10 });
              setNotice("Sales Tab code saved.");
              await load();
            }}
          >
            <input
              placeholder="Code"
              value={codeForm.code}
              onChange={(e) => setCodeForm({ ...codeForm, code: e.target.value })}
              required
            />
            <input
              placeholder="Label"
              value={codeForm.label}
              onChange={(e) => setCodeForm({ ...codeForm, label: e.target.value })}
              required
            />
            <input
              type="number"
              min={0}
              step="0.01"
              placeholder="Min"
              value={codeForm.minConsideration}
              onChange={(e) => setCodeForm({ ...codeForm, minConsideration: e.target.value })}
              required
            />
            <input
              type="number"
              min={0}
              step="0.01"
              placeholder="Max (blank = none)"
              value={codeForm.maxConsideration}
              onChange={(e) => setCodeForm({ ...codeForm, maxConsideration: e.target.value })}
            />
            <select value={codeForm.clientId} onChange={(e) => setCodeForm({ ...codeForm, clientId: e.target.value })} aria-label="Sales code Client">
              <option value="">All Clients</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.name}
                </option>
              ))}
            </select>
            <button className="primary" type="submit">
              Add code
            </button>
          </form>
        </section>
      )}

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
              onClick={() => {
                const config = configForDoc(pushDoc);
                if (config?.hasAnyReset) {
                  setPendingPush(pushDoc);
                  return;
                }
                void runPush(pushDoc).catch((err) => setError(err instanceof Error ? err.message : "Software push failed."));
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
      {pendingCode && (
        <ConfirmSheet
          title={`Remove Sales Tab code ${pendingCode.code}?`}
          body="Editors will no longer be able to assign this code on the Sales page."
          confirmLabel="Remove"
          onCancel={() => setPendingCode(null)}
          onConfirm={async () => {
            await endpoints.deleteSalesTabCode(pendingCode.id);
            setPendingCode(null);
            setNotice("Sales Tab code removed.");
            await load();
          }}
        />
      )}
      {pendingPush && (
        <ConfirmSheet
          title="Push will reset Software properties"
          body={`This Client is set to reset ${resetLabels(configForDoc(pendingPush) ?? draft!).join(", ")} on push. Continue?`}
          confirmLabel="Push and reset"
          onCancel={() => setPendingPush(null)}
          onConfirm={async () => {
            const id = pendingPush;
            setPendingPush(null);
            await runPush(id);
          }}
        />
      )}
    </section>
  );
}
