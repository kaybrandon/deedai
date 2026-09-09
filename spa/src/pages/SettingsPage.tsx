import { FormEvent, useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  DEED_FIELDS,
  endpoints,
  type ClientItem,
  type DeedTypeItem,
  type FlagItem,
  type NotificationSettings,
  type PropertyDefaultItem,
  type SoftwareSettings,
  type StatusItem,
  type TeamItem,
  type UserSummary
} from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";

type PendingDelete =
  | { kind: "flag"; id: string; name: string }
  | { kind: "status"; id: string; name: string }
  | { kind: "deedType"; id: string; name: string }
  | { kind: "team"; id: string; name: string }
  | { kind: "client"; id: string; name: string }
  | { kind: "property"; id: string; name: string }
  | { kind: "reset"; scope: "Client" | "DeedType"; clientId?: string; deedType?: string; name: string }
  | { kind: "purge" };

export default function SettingsPage() {
  const { canAdmin } = useAuth();
  const navigate = useNavigate();
  const [flags, setFlags] = useState<FlagItem[]>([]);
  const [statuses, setStatuses] = useState<StatusItem[]>([]);
  const [deedTypes, setDeedTypes] = useState<DeedTypeItem[]>([]);
  const [teams, setTeams] = useState<TeamItem[]>([]);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [notifications, setNotifications] = useState<NotificationSettings | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingDelete | null>(null);
  const [flagForm, setFlagForm] = useState({ name: "", color: "#3730a3", sortOrder: 10, isActive: true });
  const [statusForm, setStatusForm] = useState({ code: "", displayName: "", color: "#1d4ed8", sortOrder: 10, isActive: true });
  const [mapForm, setMapForm] = useState({ deedType: "", softwareCode: "", fieldMapJson: "", isActive: true });
  const [teamForm, setTeamForm] = useState({ name: "", isActive: true, userIds: [] as string[] });
  const [clientForm, setClientForm] = useState({ name: "", isActive: true });
  const [software, setSoftware] = useState<SoftwareSettings | null>(null);
  const [defaults, setDefaults] = useState<PropertyDefaultItem[]>([]);
  const [defaultForm, setDefaultForm] = useState({
    scope: "Client",
    clientId: "",
    deedType: "",
    fieldKey: "client",
    defaultValue: ""
  });
  const [resetScope, setResetScope] = useState<"Client" | "DeedType">("Client");
  const [resetClientId, setResetClientId] = useState("");
  const [resetDeedType, setResetDeedType] = useState("");

  async function load() {
    const [nextFlags, nextStatuses, nextMaps, nextTeams, nextClients, nextUsers, nextNotify, nextSoftware, nextDefaults] =
      await Promise.all([
        endpoints.flags(),
        endpoints.statuses(),
        endpoints.deedTypes(),
        endpoints.teams(),
        endpoints.settingsClients(),
        endpoints.users(),
        endpoints.notifications(),
        endpoints.softwareSettings(),
        endpoints.propertyDefaults()
      ]);
    setFlags(nextFlags);
    setStatuses(nextStatuses);
    setDeedTypes(nextMaps);
    setTeams(nextTeams);
    setClients(nextClients);
    setUsers(nextUsers);
    setNotifications(nextNotify);
    setSoftware(nextSoftware);
    setDefaults(nextDefaults);
  }

  useEffect(() => {
    if (!canAdmin) {
      navigate("/denied", { state: { action: "change settings" } });
      return;
    }
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load settings."));
  }, [canAdmin, navigate]);

  async function confirmDelete() {
    if (!pending) return;
    try {
      if (pending.kind === "flag") await endpoints.deleteFlag(pending.id);
      if (pending.kind === "status") await endpoints.deleteStatus(pending.id);
      if (pending.kind === "deedType") await endpoints.deleteDeedType(pending.id);
      if (pending.kind === "team") await endpoints.deleteTeam(pending.id);
      if (pending.kind === "client") await endpoints.deleteClient(pending.id);
      if (pending.kind === "property") await endpoints.deletePropertyDefault(pending.id);
      if (pending.kind === "reset") {
        await endpoints.resetPropertyDefaults({
          scope: pending.scope,
          clientId: pending.clientId || null,
          deedType: pending.deedType || null
        });
      }
      if (pending.kind === "purge") await endpoints.purgeDeleted("");
      setNotice(pending.kind === "purge" ? "Deleted deeds purged." : pending.kind === "reset" ? `${pending.name} reset.` : `${pending.name} removed.`);
      setError(null);
      setPending(null);
      await load();
    } catch (err) {
      setPending(null);
      setError(err instanceof Error ? err.message : "Remove failed.");
    }
  }

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

      <section className="panel">
        <h2>Manage Documents</h2>
        <p className="muted">Restore and hard-delete live on the Restore page. Purge permanently removes every soft-deleted deed. Failed OCR requeue is Workstream B.</p>
        <div className="row-actions">
          <button className="primary" type="button" onClick={() => navigate("/restore")}>
            Open Restore
          </button>
          <button className="danger" type="button" onClick={() => setPending({ kind: "purge" })}>
            Purge deleted deeds
          </button>
        </div>
      </section>

      {software && (
        <section className="panel">
          <h2>Software defaults</h2>
          <p className="muted">
            Enable push and set field/group defaults used on Software push. Staff field maps are on the{" "}
            <button className="link" type="button" onClick={() => navigate("/software")}>
              Software
            </button>{" "}
            page. Deed-type maps below stay as advanced Settings.
          </p>
          <label className="remember">
            <input
              type="checkbox"
              checked={software.pushEnabled}
              onChange={async (e) => {
                const next = await endpoints.updateSoftwareSettings({
                  pushEnabled: e.target.checked,
                  defaultGroup: software.defaultGroup,
                  fieldDefaultsJson: software.fieldDefaultsJson
                });
                setSoftware(next);
                setNotice(next.pushEnabled ? "Software push enabled." : "Software push disabled.");
              }}
            />
            Enable Software push
          </label>
          <form
            className="inline-form"
            onSubmit={async (event: FormEvent) => {
              event.preventDefault();
              const next = await endpoints.updateSoftwareSettings({
                pushEnabled: software.pushEnabled,
                defaultGroup: software.defaultGroup,
                fieldDefaultsJson: software.fieldDefaultsJson
              });
              setSoftware(next);
              setNotice("Software defaults saved.");
            }}
          >
            <input
              placeholder="Default Software group"
              value={software.defaultGroup ?? ""}
              onChange={(e) => setSoftware({ ...software, defaultGroup: e.target.value })}
            />
            <input
              placeholder='Field defaults JSON e.g. {"consideration":"0"}'
              value={software.fieldDefaultsJson ?? ""}
              onChange={(e) => setSoftware({ ...software, fieldDefaultsJson: e.target.value })}
            />
            <button className="primary" type="submit">
              Save defaults
            </button>
          </form>
        </section>
      )}

      <SettingsBlock
        title="Property defaults"
        empty={defaults.length === 0}
        emptyBody="Set mapped field defaults per Client or deed type. Reset clears that scope."
      >
        <ul className="setting-list">
          {defaults.map((item) => (
            <li key={item.id}>
              <span>
                {item.scope === "Client" ? item.clientName : item.deedType} · {item.fieldKey} = {item.defaultValue ?? "—"}
              </span>
              <button
                className="link"
                type="button"
                onClick={() => setPending({ kind: "property", id: item.id, name: `${item.fieldKey} default` })}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createPropertyDefault({
              scope: defaultForm.scope,
              clientId: defaultForm.clientId || null,
              deedType: defaultForm.deedType || null,
              fieldKey: defaultForm.fieldKey,
              defaultValue: defaultForm.defaultValue
            });
            setDefaultForm({ scope: "Client", clientId: "", deedType: "", fieldKey: "client", defaultValue: "" });
            setNotice("Property default saved.");
            await load();
          }}
        >
          <select
            value={defaultForm.scope}
            onChange={(e) => setDefaultForm({ ...defaultForm, scope: e.target.value })}
            aria-label="Default scope"
          >
            <option>Client</option>
            <option>DeedType</option>
          </select>
          {defaultForm.scope === "Client" ? (
            <select
              value={defaultForm.clientId}
              onChange={(e) => setDefaultForm({ ...defaultForm, clientId: e.target.value })}
              aria-label="Default Client"
              required
            >
              <option value="">Client</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.name}
                </option>
              ))}
            </select>
          ) : (
            <select
              value={defaultForm.deedType}
              onChange={(e) => setDefaultForm({ ...defaultForm, deedType: e.target.value })}
              aria-label="Default deed type"
              required
            >
              <option value="">Deed type</option>
              {deedTypes.map((item) => (
                <option key={item.id} value={item.deedType}>
                  {item.deedType}
                </option>
              ))}
            </select>
          )}
          <select
            value={defaultForm.fieldKey}
            onChange={(e) => setDefaultForm({ ...defaultForm, fieldKey: e.target.value })}
            aria-label="Default field"
          >
            {DEED_FIELDS.map((field) => (
              <option key={field}>{field}</option>
            ))}
          </select>
          <input
            placeholder="Default value"
            value={defaultForm.defaultValue}
            onChange={(e) => setDefaultForm({ ...defaultForm, defaultValue: e.target.value })}
          />
          <button className="primary" type="submit">
            Add default
          </button>
        </form>
        <div className="inline-form">
          <select value={resetScope} onChange={(e) => setResetScope(e.target.value as "Client" | "DeedType")} aria-label="Reset scope">
            <option>Client</option>
            <option>DeedType</option>
          </select>
          {resetScope === "Client" ? (
            <select value={resetClientId} onChange={(e) => setResetClientId(e.target.value)} aria-label="Reset Client">
              <option value="">Client to reset</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.name}
                </option>
              ))}
            </select>
          ) : (
            <select value={resetDeedType} onChange={(e) => setResetDeedType(e.target.value)} aria-label="Reset deed type">
              <option value="">Deed type to reset</option>
              {deedTypes.map((item) => (
                <option key={item.id} value={item.deedType}>
                  {item.deedType}
                </option>
              ))}
            </select>
          )}
          <button
            className="danger"
            type="button"
            onClick={() =>
              setPending({
                kind: "reset",
                scope: resetScope,
                clientId: resetClientId || undefined,
                deedType: resetDeedType || undefined,
                name: resetScope === "Client" ? "Client property defaults" : "deed-type property defaults"
              })
            }
          >
            Reset defaults
          </button>
        </div>
      </SettingsBlock>

      {notifications && (
        <section className="panel">
          <h2>Notify emails</h2>
          <p className="muted">
            SendGrid sends <strong>OCR Failed</strong> and <strong>Ready</strong> mail. Recipients: {notifications.recipientsSummary} Events:{" "}
            {notifications.events.join(" · ")}. API key is <code>SendGridApiKey</code> / <code>SendGrid__ApiKey</code> from App Settings or Key Vault only.
          </p>
          <label className="remember">
            <input
              type="checkbox"
              checked={notifications.enabled}
              onChange={async (e) => {
                const next = await endpoints.updateNotifications({
                  enabled: e.target.checked,
                  notifyUploader: notifications.notifyUploader
                });
                setNotifications(next);
                setNotice(next.enabled ? "Notify emails on." : "Notify emails off.");
              }}
            />
            Send OCR notify emails
          </label>
          <label className="remember">
            <input
              type="checkbox"
              checked={notifications.notifyUploader}
              onChange={async (e) => {
                const next = await endpoints.updateNotifications({
                  enabled: notifications.enabled,
                  notifyUploader: e.target.checked
                });
                setNotifications(next);
                setNotice("Uploader notify option saved.");
              }}
            />
            Also notify the uploader (optional)
          </label>
        </section>
      )}

      <SettingsBlock title="Clients" empty={clients.length === 0} emptyBody="Add a Client (never County). Inactive Clients stay off upload and report filters.">
        <ul className="setting-list">
          {clients.map((client) => (
            <li key={client.id}>
              <span>
                {client.name} <span className="muted">{client.isActive === false ? "· inactive" : ""}</span>
              </span>
              <div className="row-actions">
                <button
                  className="link"
                  type="button"
                  onClick={() =>
                    void endpoints
                      .updateClient(client.id, { name: client.name, isActive: !(client.isActive !== false) })
                      .then(load)
                  }
                >
                  {client.isActive === false ? "Activate" : "Deactivate"}
                </button>
                <button className="link" type="button" onClick={() => setPending({ kind: "client", id: client.id, name: client.name })}>
                  Remove
                </button>
              </div>
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createClient(clientForm);
            setClientForm({ name: "", isActive: true });
            setNotice("Client saved.");
            await load();
          }}
        >
          <input placeholder="Client name" value={clientForm.name} onChange={(e) => setClientForm({ ...clientForm, name: e.target.value })} required />
          <button className="primary" type="submit">
            Add Client
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock title="Teams" empty={teams.length === 0} emptyBody="Create named teams and assign users. This is not the full legacy security-policy matrix.">
        <ul className="setting-list">
          {teams.map((team) => (
            <li key={team.id}>
              <span>
                {team.name}{" "}
                <span className="muted">
                  ({team.members.map((m) => m.displayName).join(", ") || "no members"}
                  {team.isActive ? "" : " · inactive"})
                </span>
              </span>
              <button className="link" type="button" onClick={() => setPending({ kind: "team", id: team.id, name: team.name })}>
                Remove
              </button>
            </li>
          ))}
        </ul>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createTeam(teamForm);
            setTeamForm({ name: "", isActive: true, userIds: [] });
            setNotice("Team saved.");
            await load();
          }}
        >
          <input placeholder="Team name" value={teamForm.name} onChange={(e) => setTeamForm({ ...teamForm, name: e.target.value })} required />
          <select
            multiple
            value={teamForm.userIds}
            aria-label="Team members"
            onChange={(e) => setTeamForm({ ...teamForm, userIds: [...e.target.selectedOptions].map((o) => o.value) })}
          >
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.displayName}
              </option>
            ))}
          </select>
          <button className="primary" type="submit">
            Add team
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock title="Flags" empty={flags.length === 0} emptyBody="Create review flags the team can apply on a deed.">
        <ul className="setting-list">
          {flags.map((flag) => (
            <li key={flag.id}>
              <span className="flag-pill" style={{ background: flag.color }}>
                {flag.name}
              </span>
              <button className="link" type="button" onClick={() => setPending({ kind: "flag", id: flag.id, name: flag.name })}>
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

      <SettingsBlock title="Statuses" empty={statuses.length === 0} emptyBody="Pipeline and review statuses appear in filters and reports.">
        <ul className="setting-list">
          {statuses.map((status) => (
            <li key={status.id}>
              <span>
                {status.displayName}{" "}
                <span className="muted">
                  ({status.code}
                  {status.isSystem ? " · system" : ""})
                </span>
              </span>
              {!status.isSystem && (
                <button className="link" type="button" onClick={() => setPending({ kind: "status", id: status.id, name: status.displayName })}>
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

      <SettingsBlock title="Deed-type maps" empty={deedTypes.length === 0} emptyBody="Map deed types to Software codes used on lookup and push.">
        <ul className="setting-list">
          {deedTypes.map((map) => (
            <li key={map.id}>
              <span>
                {map.deedType} → {map.softwareCode}
              </span>
              <button className="link" type="button" onClick={() => setPending({ kind: "deedType", id: map.id, name: map.deedType })}>
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
          <input placeholder="Field map JSON (optional)" value={mapForm.fieldMapJson} onChange={(e) => setMapForm({ ...mapForm, fieldMapJson: e.target.value })} />
          <button className="primary" type="submit">
            Add map
          </button>
        </form>
      </SettingsBlock>

      {pending && (
        <ConfirmSheet
          title={
            pending.kind === "purge"
              ? "Purge all deleted deeds?"
              : pending.kind === "reset"
                ? `Reset ${pending.name}?`
                : `Remove ${pending.name}?`
          }
          body={
            pending.kind === "purge"
              ? "Permanently delete every soft-deleted deed. This cannot be undone."
              : pending.kind === "reset"
                ? "All mapped field defaults for that Client or deed type will be removed."
                : "This Settings item will be deleted. Cancel if you are not sure."
          }
          confirmLabel={pending.kind === "purge" ? "Purge" : pending.kind === "reset" ? "Reset" : "Remove"}
          onCancel={() => setPending(null)}
          onConfirm={() => void confirmDelete()}
        />
      )}
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
