import { FormEvent, useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  endpoints,
  type ClientItem,
  type DeedTypeItem,
  type FlagItem,
  type NotificationSettings,
  type OcrCleanupItem,
  type SessionConfig,
  type SoftwareSettings,
  type StatusItem,
  type TeamItem,
  type UserSummary
} from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import { FieldHelp, LabelWithHelp } from "../components/FieldHelp";
import SwaggerAdminPanel from "../components/SwaggerAdminPanel";
import SystemHealthPanel from "../components/SystemHealthPanel";
import type { HelpKey } from "../helpCatalog";

type PendingDelete =
  | { kind: "flag"; id: string; name: string }
  | { kind: "status"; id: string; name: string }
  | { kind: "deedType"; id: string; name: string }
  | { kind: "team"; id: string; name: string }
  | { kind: "client"; id: string; name: string }
  | { kind: "ocr"; id: string; name: string }
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
  const [session, setSession] = useState<SessionConfig | null>(null);
  const [ocrRules, setOcrRules] = useState<OcrCleanupItem[]>([]);
  const [idleMinutes, setIdleMinutes] = useState(30);
  const [ocrForm, setOcrForm] = useState({ kind: "Discard", value: "", isActive: true, sortOrder: 50 });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingDelete | null>(null);
  const [flagForm, setFlagForm] = useState({ name: "", color: "#3730a3", sortOrder: 10, isActive: true });
  const [statusForm, setStatusForm] = useState({ code: "", displayName: "", color: "#1d4ed8", sortOrder: 10, isActive: true });
  const [mapForm, setMapForm] = useState({ deedType: "", softwareCode: "", fieldMapJson: "", isActive: true });
  const [teamForm, setTeamForm] = useState({ name: "", isActive: true, userIds: [] as string[] });
  const [clientForm, setClientForm] = useState({ name: "", isActive: true });
  const [software, setSoftware] = useState<SoftwareSettings | null>(null);

  async function load() {
    const [
      nextFlags,
      nextStatuses,
      nextMaps,
      nextTeams,
      nextClients,
      nextUsers,
      nextNotify,
      nextSoftware,
      nextSession,
      nextOcr
    ] = await Promise.all([
      endpoints.flags(),
      endpoints.statuses(),
      endpoints.deedTypes(),
      endpoints.teams(),
      endpoints.settingsClients(),
      endpoints.users(),
      endpoints.notifications(),
      endpoints.softwareSettings(),
      endpoints.session(),
      endpoints.ocrCleanup()
    ]);
    setFlags(nextFlags);
    setStatuses(nextStatuses);
    setDeedTypes(nextMaps);
    setTeams(nextTeams);
    setClients(nextClients);
    setUsers(nextUsers);
    setNotifications(nextNotify);
    setSoftware(nextSoftware);
    setSession(nextSession);
    setIdleMinutes(nextSession.idleTimeoutMinutes);
    setOcrRules(nextOcr);
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
      if (pending.kind === "ocr") await endpoints.deleteOcrCleanup(pending.id);
      if (pending.kind === "purge") await endpoints.purgeDeleted("");
      setNotice(pending.kind === "purge" ? "Deleted deeds purged." : `${pending.name} removed.`);
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
      <div className="page-head">
        <div>
          <h1>Systems</h1>
          <p className="page-kicker">Workspace lists, session, Software maps, and API docs.</p>
        </div>
        <div className="row-actions">
          <a className="ghost swagger-open" href="#system-health">
            System health
          </a>
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

      <SystemHealthPanel />

      <SwaggerAdminPanel />

      <section className="panel">
        <h2>Manage Documents</h2>
        <p className="muted">Restore and hard-delete live on the Restore page. Purge permanently removes every soft-deleted deed. Failed and JSON Retry requeue extract from Documents and Review.</p>
        <div className="row-actions">
          <button className="primary" type="button" onClick={() => navigate("/restore")}>
            Open Restore
          </button>
          <button className="danger" type="button" onClick={() => setPending({ kind: "purge" })}>
            Purge Deleted Deeds
          </button>
        </div>
      </section>

      {session && (
        <section className="panel">
          <h2>
            Session Idle Timeout <FieldHelp helpKey="settings.idleTimeout" />
          </h2>
          <p className="muted">
            Default is <strong>{session.defaultMinutes} minutes</strong> (App Setting{" "}
            <code>Session__IdleTimeoutMinutes</code> / Admin Settings). After idle expiry the SPA signs you out and
            sends you to login with a reason — unsaved draft field edits are not silently wiped.
          </p>
          <form
            className="inline-form"
            onSubmit={async (event: FormEvent) => {
              event.preventDefault();
              const next = await endpoints.updateSession(idleMinutes);
              setSession(next);
              setIdleMinutes(next.idleTimeoutMinutes);
              setNotice(`Idle timeout saved: ${next.idleTimeoutMinutes} minutes.`);
            }}
          >
            <input
              type="number"
              min={5}
              max={1440}
              value={idleMinutes}
              aria-label="Idle timeout minutes"
              onChange={(e) => setIdleMinutes(Number(e.target.value))}
            />
            <button className="primary" type="submit">
              Save Timeout
            </button>
          </form>
        </section>
      )}

      {software && (
        <section className="panel">
          <h2>Software Defaults</h2>
          <p className="muted">
            Enable push and the fallback Software group here. Vendor, API URL, group code, Sales Tab, date/label depth,
            and property resets are typed per Client on the{" "}
            <button className="link" type="button" onClick={() => navigate("/software")}>
              Software
            </button>{" "}
            page. The API key stays in Key Vault and shows as Configured there — never as a secret. Deed-type maps below
            stay as advanced Settings.
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
            <LabelWithHelp helpKey="software.enablePush">Enable Software Push</LabelWithHelp>
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
              className="form-wide"
              placeholder='Advanced field defaults JSON e.g. {"consideration":"0"}'
              value={software.fieldDefaultsJson ?? ""}
              onChange={(e) => setSoftware({ ...software, fieldDefaultsJson: e.target.value })}
            />
            <button className="primary" type="submit">
              Save Defaults
            </button>
          </form>
        </section>
      )}

      <SettingsBlock
        title="OCR Trim / Discard"
        helpKey="settings.ocrTrim"
        empty={ocrRules.length === 0}
        emptyBody="Seeded trim characters and discard words clean new extracts. Add more here — never put secrets in this list."
      >
        <div className="token-row">
          {ocrRules.map((rule) => (
            <span
              key={rule.id}
              className={`token token-${rule.kind.toLowerCase()}${rule.isActive ? "" : " is-inactive"}`}
            >
              <span className="token-kind">{rule.kind}</span>
              <code className="token-value">{rule.value}</code>
              <button
                className="token-remove"
                type="button"
                aria-label={`Remove ${rule.kind} ${rule.value}`}
                onClick={() => setPending({ kind: "ocr", id: rule.id, name: `${rule.kind} ${rule.value}` })}
              >
                ×
              </button>
            </span>
          ))}
        </div>
        <form
          className="inline-form"
          onSubmit={async (event: FormEvent) => {
            event.preventDefault();
            await endpoints.createOcrCleanup(ocrForm);
            setOcrForm({ kind: "Discard", value: "", isActive: true, sortOrder: 50 });
            setNotice("OCR cleanup rule saved.");
            await load();
          }}
        >
          <select value={ocrForm.kind} onChange={(e) => setOcrForm({ ...ocrForm, kind: e.target.value })} aria-label="Cleanup kind">
            <option value="Trim">Trim</option>
            <option value="Discard">Discard</option>
          </select>
          <input
            placeholder={ocrForm.kind === "Trim" ? "Character" : "Word"}
            value={ocrForm.value}
            onChange={(e) => setOcrForm({ ...ocrForm, value: e.target.value })}
            required
          />
          <button className="primary" type="submit">
            Add Rule
          </button>
        </form>
      </SettingsBlock>

      {notifications && (
        <section className="panel">
          <h2>Notify Emails</h2>
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
            Send OCR Notify Emails
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

      <SettingsBlock title="Clients" empty={clients.length === 0} emptyBody="Add a Client. Inactive Clients stay off upload and report filters.">
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
            Add Team
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock title="Flags" helpKey="settings.flags" empty={flags.length === 0} emptyBody="Create review flags the team can apply on a deed.">
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
            Add Flag
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock title="Statuses" helpKey="settings.statuses" empty={statuses.length === 0} emptyBody="Pipeline and review statuses appear in filters and reports.">
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
            Add Status
          </button>
        </form>
      </SettingsBlock>

      <SettingsBlock title="Deed-Type Maps" helpKey="settings.deedTypeMaps" empty={deedTypes.length === 0} emptyBody="Map deed types to Software codes used on lookup and push.">
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
            Add Map
          </button>
        </form>
      </SettingsBlock>

      {pending && (
        <ConfirmSheet
          title={pending.kind === "purge" ? "Purge all deleted deeds?" : `Remove ${pending.name}?`}
          body={
            pending.kind === "purge"
              ? "Permanently delete every soft-deleted deed. This cannot be undone."
              : "This Settings item will be deleted. Cancel if you are not sure."
          }
          confirmLabel={pending.kind === "purge" ? "Purge" : "Remove"}
          onCancel={() => setPending(null)}
          onConfirm={() => void confirmDelete()}
        />
      )}
    </section>
  );
}

function SettingsBlock({
  title,
  helpKey,
  empty,
  emptyBody,
  children
}: {
  title: string;
  helpKey?: HelpKey;
  empty: boolean;
  emptyBody: string;
  children: ReactNode;
}) {
  return (
    <section className="panel">
      <h2>
        {title}
        {helpKey && <FieldHelp helpKey={helpKey} />}
      </h2>
      {empty && <EmptyState title={`No ${title.toLowerCase()} yet`} body={emptyBody} />}
      {children}
    </section>
  );
}
