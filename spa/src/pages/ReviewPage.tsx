import { FormEvent, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  endpoints,
  type DocumentDetail,
  type DocumentListItem,
  type DeedTypeItem,
  type FieldDraft,
  type FlagItem,
  type NotifyPreview,
  type SoftwareClientConfig,
  type SoftwareLookup,
  type StatusItem,
  type UserSummary
} from "../api";
import { markDraftDirty, useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import StatusChip from "../components/StatusChip";
import { displayStatus } from "../reviewStatus";

const emptyFields: FieldDraft = {
  grantor: "",
  grantee: "",
  instrumentDate: "",
  consideration: "",
  parcelId: "",
  client: "",
  notes: "",
  isDraft: true
};

export default function ReviewPage() {
  const { id } = useParams<{ id: string }>();
  const { canEdit } = useAuth();
  const navigate = useNavigate();
  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [fields, setFields] = useState<FieldDraft>(emptyFields);
  const [deedType, setDeedType] = useState("");
  const [reviewStatus, setReviewStatus] = useState("");
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [pdfState, setPdfState] = useState<"loading" | "ready" | "missing">("loading");
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [flagDefs, setFlagDefs] = useState<FlagItem[]>([]);
  const [deedTypes, setDeedTypes] = useState<DeedTypeItem[]>([]);
  const [statuses, setStatuses] = useState<StatusItem[]>([]);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [linkTarget, setLinkTarget] = useState("");
  const [teamUser, setTeamUser] = useState("");
  const [lookup, setLookup] = useState<SoftwareLookup | null>(null);
  const [notify, setNotify] = useState<NotifyPreview | null>(null);
  const [clientConfig, setClientConfig] = useState<SoftwareClientConfig | null>(null);
  const [pendingPush, setPendingPush] = useState<"push" | "retry" | null>(null);

  async function load(documentId: string) {
    const detail = await endpoints.document(documentId);
    setDoc(detail);
    setFields({ ...emptyFields, ...detail.fields, isDraft: detail.fields.isDraft });
    setDeedType(detail.deedType ?? "");
    setReviewStatus(detail.reviewStatus ?? "");
    setSaved(!detail.fields.isDraft && Boolean(detail.fields.grantor));
    endpoints.notifyPreview(documentId).then(setNotify).catch(() => setNotify(null));
    if (canEdit) {
      endpoints
        .softwareClientConfigs()
        .then((rows) => setClientConfig(rows.find((row) => row.clientId === detail.clientId) ?? null))
        .catch(() => setClientConfig(null));
    }
  }

  useEffect(() => {
    endpoints.users().then(setUsers).catch(() => undefined);
    endpoints.flags().then(setFlagDefs).catch(() => undefined);
    endpoints.deedTypes().then(setDeedTypes).catch(() => undefined);
    endpoints.statuses().then(setStatuses).catch(() => undefined);
    endpoints.documents("").then(setDocs).catch(() => undefined);
  }, []);

  useEffect(() => {
    if (id) {
      load(id).catch((err) => setError(err instanceof Error ? err.message : "Could not load deed."));
    }
  }, [id]);

  useEffect(() => {
    if (!id) return;
    let objectUrl: string | undefined;
    const token = sessionStorage.getItem("deedai.token");
    setPdfState("loading");
    fetch(`/api/documents/${id}/file`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
      .then(async (response) => {
        if (!response.ok) {
          setPdfState("missing");
          return;
        }
        objectUrl = URL.createObjectURL(await response.blob());
        setPdfUrl(objectUrl);
        setPdfState("ready");
      })
      .catch(() => {
        setPdfUrl(null);
        setPdfState("missing");
      });
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      setPdfUrl(null);
    };
  }, [id]);

  useEffect(() => {
    markDraftDirty(fields.isDraft);
    return () => markDraftDirty(false);
  }, [fields.isDraft]);

  function update<K extends keyof FieldDraft>(key: K, value: FieldDraft[K]) {
    setFields((current) => ({ ...current, [key]: value, isDraft: true }));
    setSaved(false);
  }

  async function runSoftware(kind: "push" | "retry") {
    if (!doc) return;
    const result = kind === "push" ? await endpoints.softwarePush(doc.id) : await endpoints.softwareRetry(doc.id);
    setNotice(result.succeeded ? result.message : result.failReason ?? result.message);
    if (!result.succeeded) setError(result.failReason ?? result.message);
    else setError(null);
    await load(doc.id);
  }

  async function save(event: FormEvent, asDraft: boolean) {
    event.preventDefault();
    if (!id) return;
    if (!canEdit) {
      navigate("/denied", { state: { action: "edit deed fields" } });
      return;
    }
    const next = await endpoints.saveFields(id, {
      ...fields,
      isDraft: asDraft,
      deedType,
      reviewStatus
    });
    setFields(next);
    setSaved(!next.isDraft);
    await load(id);
  }

  if (!doc) {
    return (
      <section className="page">
        {error ? <div className="denied-box">{error}</div> : <p>Loading…</p>}
      </section>
    );
  }

  const selectedFlags = new Set(doc.flags.map((flag) => flag.id));
  const reviewStatuses = statuses.filter((status) => !status.isSystem);

  return (
    <section className="page">
      <div className="review-header">
        <div className="title-row">
          <h1>Deed review</h1>
          <StatusChip
            status={displayStatus({
              status: doc.status,
              displayStatus: doc.displayStatus,
              reviewStatus: reviewStatus || doc.reviewStatus,
              flags: doc.flags
            })}
            title={doc.errorMessage}
          />
        </div>
        <div className="row-actions">
          <button className="ghost" type="button" disabled={!doc.previousId} onClick={() => doc.previousId && navigate(`/documents/${doc.previousId}`)}>
            Previous
          </button>
          <button className="ghost" type="button" disabled={!doc.nextId} onClick={() => doc.nextId && navigate(`/documents/${doc.nextId}`)}>
            Next
          </button>
          <button
            className="ghost"
            type="button"
            disabled={!canEdit}
            onClick={async () => {
              await endpoints.retry(doc.id);
              await load(doc.id);
            }}
          >
            Retry
          </button>
          <button
            className="ghost"
            type="button"
            onClick={() =>
              endpoints.exportReviewedPdf(doc.id, `${doc.name}-reviewed.pdf`).catch((err) =>
                setError(err instanceof Error ? err.message : "PDF export failed.")
              )
            }
          >
            Export PDF
          </button>
          <button className="primary" type="submit" form="field-form" disabled={!canEdit}>
            Save
          </button>
        </div>
      </div>

      {doc.status === "Failed" && (
        <div className="alert-bar">
          <span>{doc.errorMessage ?? "OCR failed — Retry extract"}</span>
          <button
            className="primary"
            type="button"
            disabled={!canEdit}
            onClick={async () => {
              await endpoints.retry(doc.id);
              await load(doc.id);
            }}
          >
            Retry extract
          </button>
        </div>
      )}
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}

      <div className="collab-row">
        <label>
          Assignee
          <select
            value={doc.assigneeUserId ?? ""}
            disabled={!canEdit}
            onChange={async (e) => {
              await endpoints.assign(doc.id, e.target.value || null);
              await load(doc.id);
            }}
          >
            <option value="">Unassigned</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.displayName}
              </option>
            ))}
          </select>
        </label>
        <label>
          Deed type
          <select value={deedType} disabled={!canEdit} onChange={(e) => setDeedType(e.target.value)}>
            <option value="">None</option>
            {deedTypes.map((item) => (
              <option key={item.id} value={item.deedType}>
                {item.deedType}
              </option>
            ))}
          </select>
        </label>
        <label>
          Review status
          <select value={reviewStatus} disabled={!canEdit} onChange={(e) => setReviewStatus(e.target.value)}>
            <option value="">None</option>
            {reviewStatuses.map((item) => (
              <option key={item.id} value={item.code}>
                {item.displayName}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="review-grid">
        <div className="pdf-pane">
          {pdfState === "ready" && pdfUrl ? (
            <iframe title="PDF preview" src={pdfUrl} />
          ) : (
            <div className="pdf-placeholder">
              {pdfState === "missing" ? "PDF is not available for this deed." : "Loading PDF…"}
            </div>
          )}
        </div>
        <form id="field-form" className="field-form" onSubmit={(e) => save(e, false)}>
          <Field label="Grantor" value={fields.grantor ?? ""} onChange={(v) => update("grantor", v)} readOnly={!canEdit} />
          <Field label="Grantee" value={fields.grantee ?? ""} onChange={(v) => update("grantee", v)} readOnly={!canEdit} />
          <Field label="Instrument date" value={fields.instrumentDate ?? ""} onChange={(v) => update("instrumentDate", v)} readOnly={!canEdit} />
          <Field label="Consideration" value={fields.consideration ?? ""} onChange={(v) => update("consideration", v)} readOnly={!canEdit} />
          <Field label="Parcel ID" value={fields.parcelId ?? ""} onChange={(v) => update("parcelId", v)} readOnly={!canEdit} />
          <Field label="Client" value={fields.client ?? ""} onChange={(v) => update("client", v)} readOnly={!canEdit} />
          <label>
            Notes
            <textarea
              rows={4}
              value={fields.notes ?? ""}
              onChange={(e) => update("notes", e.target.value)}
              readOnly={!canEdit}
            />
          </label>
          {fields.isDraft && canEdit && <p className="muted">Draft — press Save to confirm field edits.</p>}
        </form>
      </div>

      <div className="collab-grid">
        <section className="panel">
          <h2>Flags</h2>
          <div className="flag-list">
            {flagDefs.filter((flag) => flag.isActive).map((flag) => (
              <label key={flag.id} className="remember">
                <input
                  type="checkbox"
                  checked={selectedFlags.has(flag.id)}
                  disabled={!canEdit}
                  onChange={async (e) => {
                    const next = new Set(selectedFlags);
                    if (e.target.checked) next.add(flag.id);
                    else next.delete(flag.id);
                    await endpoints.setFlags(doc.id, [...next]);
                    await load(doc.id);
                  }}
                />
                <span className="flag-pill" style={{ background: flag.color }}>
                  {flag.name}
                </span>
              </label>
            ))}
          </div>
        </section>

        <section className="panel">
          <h2>Team</h2>
          <ul className="setting-list">
            {doc.team.map((member) => (
              <li key={member.id}>
                <span>
                  {member.displayName} <span className="muted">({member.role})</span>
                </span>
                {canEdit && (
                  <button className="link" type="button" onClick={() => void endpoints.removeTeam(doc.id, member.id).then(() => load(doc.id))}>
                    Remove
                  </button>
                )}
              </li>
            ))}
          </ul>
          {canEdit && (
            <div className="inline-form">
              <select value={teamUser} onChange={(e) => setTeamUser(e.target.value)} aria-label="Add team member">
                <option value="">Add teammate</option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.displayName}
                  </option>
                ))}
              </select>
              <button
                className="primary"
                type="button"
                disabled={!teamUser}
                onClick={async () => {
                  await endpoints.addTeam(doc.id, teamUser);
                  setTeamUser("");
                  await load(doc.id);
                }}
              >
                Add
              </button>
            </div>
          )}
        </section>

        <section className="panel">
          <h2>Linked documents</h2>
          <ul className="setting-list">
            {doc.linkedDocuments.map((linked) => (
              <li key={linked.id}>
                <button className="link" type="button" onClick={() => navigate(`/documents/${linked.id}`)}>
                  {linked.name}
                </button>
                {canEdit && (
                  <button className="link" type="button" onClick={() => void endpoints.unlinkDocument(doc.id, linked.id).then(() => load(doc.id))}>
                    Unlink
                  </button>
                )}
              </li>
            ))}
          </ul>
          {canEdit && (
            <div className="inline-form">
              <select value={linkTarget} onChange={(e) => setLinkTarget(e.target.value)} aria-label="Link document">
                <option value="">Link a deed</option>
                {docs
                  .filter((item) => item.id !== doc.id)
                  .map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
              </select>
              <button
                className="primary"
                type="button"
                disabled={!linkTarget}
                onClick={async () => {
                  await endpoints.linkDocument(doc.id, linkTarget);
                  setLinkTarget("");
                  await load(doc.id);
                }}
              >
                Link
              </button>
            </div>
          )}
        </section>

        <section className="panel">
          <h2>Software</h2>
          <p className="muted">Lookup by key fields (parcel, grantor, grantee, Client) or push this deed to Software.</p>
          {doc.lastSoftwareSyncAt ? (
            <p>
              Last {doc.lastSoftwareSyncDirection ?? "sync"}: <strong>{doc.lastSoftwareSyncStatus ?? "—"}</strong>
              {doc.lastSoftwareSyncFailReason ? ` — ${doc.lastSoftwareSyncFailReason}` : ""}{" "}
              <span className="muted">{new Date(doc.lastSoftwareSyncAt).toLocaleString()}</span>
              {doc.softwareRecordId ? ` · ${doc.softwareRecordId}` : ""}
            </p>
          ) : (
            <EmptyState title="No Software sync yet" body="Lookup or push to record last-sync status and fail reason on this deed." />
          )}
          <div className="row-actions">
            <button
              className="ghost"
              type="button"
              onClick={async () => {
                try {
                  setLookup(await endpoints.softwareLookup(doc.id));
                  setError(null);
                  await load(doc.id);
                } catch (err) {
                  setLookup(null);
                  setError(err instanceof Error ? err.message : "Software lookup failed.");
                  await load(doc.id);
                }
              }}
            >
              Lookup
            </button>
            {canEdit ? (
              <>
                <button
                  className="primary"
                  type="button"
                  onClick={() => {
                    if (clientConfig?.hasAnyReset) {
                      setPendingPush("push");
                      return;
                    }
                    void runSoftware("push");
                  }}
                >
                  Push
                </button>
                <button
                  className="ghost"
                  type="button"
                  onClick={() => {
                    if (clientConfig?.hasAnyReset) {
                      setPendingPush("retry");
                      return;
                    }
                    void runSoftware("retry");
                  }}
                >
                  Retry push
                </button>
              </>
            ) : (
              <button
                className="nav-disabled"
                type="button"
                onClick={() => navigate("/denied", { state: { action: "push to Software" } })}
              >
                Push
              </button>
            )}
          </div>
          {lookup ? (
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
          ) : null}
        </section>
      </div>
      {notify && (
        <section className="panel">
          <h2>Notify emails</h2>
          {!notify.enabled ? (
            <p className="muted">Admin turned notify emails off. OCR Failed and Ready mail will not send.</p>
          ) : notify.recipients.length === 0 ? (
            <EmptyState title="No recipients yet" body="Assign this deed (and optionally turn on uploader notify) so Ready / OCR Failed mail has someone to send to." />
          ) : (
            <p>
              {notify.events.join(" and ")} mail goes to{" "}
              {notify.recipients.map((r) => `${r.displayName} (${r.reason})`).join(", ")}.
            </p>
          )}
        </section>
      )}
      {saved && <span className="chip chip-ready saved-pill">Saved</span>}
      {pendingPush && doc && (
        <ConfirmSheet
          title="Push will reset Software properties"
          body="This Client is set to reset one or more Software property groups (exemptions, supplement year, sales letter, Sales Tab, agents, or mortgage codes). Continue?"
          confirmLabel="Push and reset"
          onCancel={() => setPendingPush(null)}
          onConfirm={async () => {
            const kind = pendingPush;
            setPendingPush(null);
            await runSoftware(kind);
          }}
        />
      )}
    </section>
  );
}

function Field({
  label,
  value,
  onChange,
  readOnly
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  readOnly: boolean;
}) {
  return (
    <label>
      {label}
      <input value={value} onChange={(e) => onChange(e.target.value)} readOnly={readOnly} />
    </label>
  );
}
