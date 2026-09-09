import { FormEvent, useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type DeedTypeItem, type FlagItem, type StatusItem } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";

export default function SettingsPage() {
  const { canAdmin } = useAuth();
  const navigate = useNavigate();
  const [flags, setFlags] = useState<FlagItem[]>([]);
  const [statuses, setStatuses] = useState<StatusItem[]>([]);
  const [deedTypes, setDeedTypes] = useState<DeedTypeItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [flagForm, setFlagForm] = useState({ name: "", color: "#3730a3", sortOrder: 10, isActive: true });
  const [statusForm, setStatusForm] = useState({ code: "", displayName: "", color: "#1d4ed8", sortOrder: 10, isActive: true });
  const [mapForm, setMapForm] = useState({ deedType: "", softwareCode: "", fieldMapJson: "", isActive: true });

  async function load() {
    const [nextFlags, nextStatuses, nextMaps] = await Promise.all([
      endpoints.flags(),
      endpoints.statuses(),
      endpoints.deedTypes()
    ]);
    setFlags(nextFlags);
    setStatuses(nextStatuses);
    setDeedTypes(nextMaps);
  }

  useEffect(() => {
    if (!canAdmin) {
      navigate("/denied", { state: { action: "change settings" } });
      return;
    }
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load settings."));
  }, [canAdmin, navigate]);

  return (
    <section className="page">
      <div className="review-header">
        <h1>Settings</h1>
        <div className="row-actions">
          <button className="ghost" type="button" onClick={() => endpoints.exportSettings("json").catch((e) => setError(e.message))}>
            Export JSON
          </button>
          <button className="ghost" type="button" onClick={() => endpoints.exportSettings("csv").catch((e) => setError(e.message))}>
            Export CSV
          </button>
          <button className="primary" type="button" onClick={() => endpoints.exportSettings("xlsx").catch((e) => setError(e.message))}>
            Export Excel
          </button>
        </div>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}

      <SettingsBlock
        title="Flags"
        empty={flags.length === 0}
        emptyBody="Create review flags the team can apply on a deed."
      >
        <ul className="setting-list">
          {flags.map((flag) => (
            <li key={flag.id}>
              <span className="flag-pill" style={{ background: flag.color }}>
                {flag.name}
              </span>
              <button className="link" type="button" onClick={() => void endpoints.deleteFlag(flag.id).then(load)}>
                Remove
              </button>
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createFlag(flagForm);
            setFlagForm({ name: "", color: "#3730a3", sortOrder: 10, isActive: true });
            setNotice("Flag saved.");
            await load();
          }}
        >
          <input placeholder="Flag name" value={flagForm.name} onChange={(e) => setFlagForm({ ...flagForm, name: e.target.value })} required />
          <input type="color" value={flagForm.color} onChange={(e) => setFlagForm({ ...flagForm, color: e.target.value })} />
          <button className="primary" type="submit">
            Add flag
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock
        title="Statuses"
        empty={statuses.length === 0}
        emptyBody="Pipeline and review statuses appear in filters and reports."
      >
        <ul className="setting-list">
          {statuses.map((status) => (
            <li key={status.id}>
              <span>
                {status.displayName} <span className="muted">({status.code}{status.isSystem ? " · system" : ""})</span>
              </span>
              {!status.isSystem && (
                <button className="link" type="button" onClick={() => void endpoints.deleteStatus(status.id).then(load)}>
                  Remove
                </button>
              )}
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createStatus(statusForm);
            setStatusForm({ code: "", displayName: "", color: "#1d4ed8", sortOrder: 10, isActive: true });
            setNotice("Status saved.");
            await load();
          }}
        >
          <input placeholder="Code" value={statusForm.code} onChange={(e) => setStatusForm({ ...statusForm, code: e.target.value })} required />
          <input placeholder="Display name" value={statusForm.displayName} onChange={(e) => setStatusForm({ ...statusForm, displayName: e.target.value })} required />
          <button className="primary" type="submit">
            Add status
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock
        title="Deed-type maps"
        empty={deedTypes.length === 0}
        emptyBody="Map deed types to Software codes used on lookup and push."
      >
        <ul className="setting-list">
          {deedTypes.map((map) => (
            <li key={map.id}>
              <span>
                {map.deedType} → {map.softwareCode}
              </span>
              <button className="link" type="button" onClick={() => void endpoints.deleteDeedType(map.id).then(load)}>
                Remove
              </button>
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createDeedType(mapForm);
            setMapForm({ deedType: "", softwareCode: "", fieldMapJson: "", isActive: true });
            setNotice("Deed-type map saved.");
            await load();
          }}
        >
          <input placeholder="Deed type" value={mapForm.deedType} onChange={(e) => setMapForm({ ...mapForm, deedType: e.target.value })} required />
          <input placeholder="Software code" value={mapForm.softwareCode} onChange={(e) => setMapForm({ ...mapForm, softwareCode: e.target.value })} required />
          <input placeholder='Field map JSON (optional)' value={mapForm.fieldMapJson} onChange={(e) => setMapForm({ ...mapForm, fieldMapJson: e.target.value })} />
          <button className="primary" type="submit">
            Add map
          </button>
        </form>
      </SettingsBlock>
    </section>
  );
}

function SettingsBlock({
  title,
  empty,
  emptyBody,
  children
}: {
  title: string;
  empty: boolean;
  emptyBody: string;
  children: ReactNode;
}) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {empty && <EmptyState title={`No ${title.toLowerCase()} yet`} body={emptyBody} />}
      {children}
    </section>
  );
}
