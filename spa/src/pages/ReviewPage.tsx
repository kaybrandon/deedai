import { FormEvent, useEffect, useMemo, useState } from "react";
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
import { LabelWithHelp } from "../components/FieldHelp";
import OcrRibbon from "../components/OcrRibbon";
import StatusChip from "../components/StatusChip";
import { displayStatus } from "../reviewStatus";
import { ribbonStepForDocument } from "../theme";

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

type PartyKind = "grantor" | "grantee";

function partiesFrom(detail: DocumentDetail, kind: PartyKind): string[] {
  const list = kind === "grantor" ? detail.grantors : detail.grantees;
  const legacy = kind === "grantor" ? detail.fields.grantor : detail.fields.grantee;
  if (list && list.length > 0) {
    return [...list];
  }
  if (legacy) {
    return [legacy];
  }
  return [""];
}

function hasEmptyPartyRows(rows: string[]): boolean {
  const empty = rows.filter((row) => row.trim() === "").length;
  return empty > 0 && !(empty === rows.length && rows.length === 1);
}

function extraOf(row: SoftwareLookup, key: string): string {
  return row.extra?.[key] ?? "";
}

function inReviewQueue(item: DocumentListItem, currentId: string): boolean {
  if (item.id === currentId) {
    return true;
  }
  const shown = displayStatus(item);
  return item.status === "Failed" || item.status === "Ready" || shown === "NeedsReview";
}

export default function ReviewPage() {
  const { id } = useParams<{ id: string }>();
  const { canEdit } = useAuth();
  const navigate = useNavigate();
  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [fields, setFields] = useState<FieldDraft>(emptyFields);
  const [documentNumber, setDocumentNumber] = useState("");
  const [volume, setVolume] = useState("");
  const [page, setPage] = useState("");
  const [pid, setPid] = useState("");
  const [mailingStreet, setMailingStreet] = useState("");
  const [mailingCity, setMailingCity] = useState("");
  const [mailingState, setMailingState] = useState("");
  const [mailingZip, setMailingZip] = useState("");
  const [grantors, setGrantors] = useState<string[]>([""]);
  const [grantees, setGrantees] = useState<string[]>([""]);
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
  const [softwareQuery, setSoftwareQuery] = useState("");
  const [softwareResults, setSoftwareResults] = useState<SoftwareLookup[]>([]);
  const [softwareSearched, setSoftwareSearched] = useState(false);
  const [notify, setNotify] = useState<NotifyPreview | null>(null);
  const [clientConfig, setClientConfig] = useState<SoftwareClientConfig | null>(null);
  const [pendingPush, setPendingPush] = useState(false);
  const [pendingRemove, setPendingRemove] = useState<{ kind: PartyKind; index: number } | null>(null);

  function hydrate(detail: DocumentDetail) {
    setDoc(detail);
    setFields({ ...emptyFields, ...detail.fields, isDraft: detail.fields.isDraft });
    setPid(detail.pid || detail.fields.parcelId || "");
    setMailingStreet(detail.mailingStreet || "");
    setMailingCity(detail.mailingCity || "");
    setMailingState(detail.mailingState || "");
    setMailingZip(detail.mailingZip || "");
    setDocumentNumber(detail.documentNumber || "");
    setVolume(detail.volume || "");
    setPage(detail.page || "");
    setGrantors(partiesFrom(detail, "grantor"));
    setGrantees(partiesFrom(detail, "grantee"));
    setDeedType(detail.deedType ?? "");
    setReviewStatus(detail.reviewStatus ?? "");
    setSaved(!detail.fields.isDraft && Boolean(detail.fields.grantor || detail.grantors?.length));
  }

  async function load(documentId: string) {
    const detail = await endpoints.document(documentId);
    hydrate(detail);
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

  function markDirty() {
    setFields((current) => ({ ...current, isDraft: true }));
    setSaved(false);
  }

  function update<K extends keyof FieldDraft>(key: K, value: FieldDraft[K]) {
    setFields((current) => ({ ...current, [key]: value, isDraft: true }));
    setSaved(false);
  }

  function setParty(kind: PartyKind, index: number, value: string) {
    const setter = kind === "grantor" ? setGrantors : setGrantees;
    setter((current) => current.map((row, i) => (i === index ? value : row)));
    markDirty();
  }

  function addParty(kind: PartyKind) {
    const setter = kind === "grantor" ? setGrantors : setGrantees;
    setter((current) => [...current, ""]);
    markDirty();
  }

  function removeParty(kind: PartyKind, index: number) {
    const setter = kind === "grantor" ? setGrantors : setGrantees;
    setter((current) => {
      const next = current.filter((_, i) => i !== index);
      return next.length === 0 ? [""] : next;
    });
    markDirty();
  }

  function requestRemove(kind: PartyKind, index: number, value: string) {
    if (value.trim()) {
      setPendingRemove({ kind, index });
      return;
    }
    removeParty(kind, index);
  }

  async function runSoftwarePush() {
    if (!doc) return;
    const result = await endpoints.softwarePush(doc.id);
    setNotice(result.succeeded ? result.message : result.failReason ?? result.message);
    if (!result.succeeded) setError(result.failReason ?? result.message);
    else setError(null);
    await load(doc.id);
  }

  function applySoftware(row: SoftwareLookup) {
    const owner = row.owner?.trim() ?? "";
    const nextPid = extraOf(row, "pid") || row.parcelId;
    setPid(nextPid);
    setMailingStreet(extraOf(row, "mailingStreet") || row.address || "");
    setMailingCity(extraOf(row, "mailingCity"));
    setMailingState(extraOf(row, "mailingState"));
    setMailingZip(extraOf(row, "mailingZip"));
    update("parcelId", nextPid);
    update("client", extraOf(row, "client") || fields.client);
    if (owner) {
      setGrantors([owner]);
    }
    const grantee = extraOf(row, "grantee");
    if (grantee) {
      setGrantees([grantee]);
    }
    markDirty();
    setNotice("Software record applied to key property fields.");
    setError(null);
  }

  async function searchSoftware(event: FormEvent) {
    event.preventDefault();
    if (!doc) return;
    const query = softwareQuery.trim();
    if (!query) {
      setError("Enter a parcel ID or owner name to search Software.");
      return;
    }
    const client = encodeURIComponent(doc.client);
    const year = clientConfig?.defaultYear ?? clientConfig?.certifiedYear;
    const image = clientConfig?.lookupImageCode?.trim();
    const extras = `${year ? `&year=${year}` : ""}${image ? `&imageCode=${encodeURIComponent(image)}` : ""}`;
    const seen = new Set<string>();
    const results: SoftwareLookup[] = [];
    for (const params of [
      `?parcelId=${encodeURIComponent(query)}&client=${client}${extras}`,
      `?grantor=${encodeURIComponent(query)}&client=${client}${extras}`,
      `?grantee=${encodeURIComponent(query)}&client=${client}${extras}`
    ]) {
      try {
        const row = await endpoints.softwareLookupKeys(params);
        const key = row.softwareRecordId ?? row.parcelId;
        if (!seen.has(key)) {
          seen.add(key);
          results.push(row);
        }
      } catch {
        /* miss — try the next key field */
      }
    }
    setSoftwareResults(results);
    setSoftwareSearched(true);
    if (results.length === 0) {
      setError("No Software record found for those key fields.");
    } else {
      setError(null);
    }
  }

  async function save(event: FormEvent, asDraft: boolean) {
    event.preventDefault();
    if (!id) return;
    if (!canEdit) {
      navigate("/denied", { state: { action: "edit deed fields" } });
      return;
    }
    if (hasEmptyPartyRows(grantors)) {
      setError("Fill or remove empty grantor rows.");
      return;
    }
    if (hasEmptyPartyRows(grantees)) {
      setError("Fill or remove empty grantee rows.");
      return;
    }
    const next = await endpoints.saveFields(id, {
      ...fields,
      grantor: grantors.find((row) => row.trim()) ?? "",
      grantee: grantees.find((row) => row.trim()) ?? "",
      parcelId: pid,
      isDraft: asDraft,
      deedType,
      reviewStatus,
      documentNumber,
      volume,
      page,
      pid,
      mailingStreet,
      mailingCity,
      mailingState,
      mailingZip,
      grantors,
      grantees
    });
    setFields(next);
    setSaved(!next.isDraft);
    setError(null);
    await load(id);
  }

  const queue = useMemo(
    () => docs.filter((item) => doc && inReviewQueue(item, doc.id)),
    [docs, doc]
  );

  if (!doc) {
    return (
      <section className="page">
        {error ? <div className="denied-box">{error}</div> : <p>Loading…</p>}
      </section>
    );
  }

  const selectedFlags = new Set(doc.flags.map((flag) => flag.id));
  const reviewStatuses = statuses.filter((status) => !status.isSystem);
  const shownStatus = displayStatus({
    status: doc.status,
    displayStatus: doc.displayStatus,
    reviewStatus: reviewStatus || doc.reviewStatus,
    flags: doc.flags
  });
  const isFailed = doc.status === "Failed";
  const showFailedBanner = isFailed && shownStatus !== "Ready" && shownStatus !== "Approved";

  return (
    <section className="page has-ocr-ribbon">
      <OcrRibbon current={ribbonStepForDocument(doc.status, shownStatus)} />
      <div className="review-header">
        <div className="title-row">
          <h1>Deed Review</h1>
          <StatusChip status={shownStatus} title={doc.errorMessage} />
          <span className="muted">
            {doc.name} · {doc.client}
          </span>
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

      {showFailedBanner && (
        <div className="alert-bar" data-testid="ocr-failed-banner">
          <span>{doc.errorMessage ?? "OCR failed — incomplete fields. Retry extract."}</span>
          <button
            className="primary"
            type="button"
            disabled={!canEdit}
            onClick={async () => {
              await endpoints.retry(doc.id);
              await load(doc.id);
            }}
          >
            Retry Extract
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
          Review Status
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
        <aside className="review-queue" data-testid="review-queue" aria-label="Queue">
          <div className="review-queue-head">
            <h2>Queue</h2>
            <span className="muted">{queue.length}</span>
          </div>
          {queue.length === 0 ? (
            <EmptyState title="No Documents Yet" body="Deeds that need review appear here." />
          ) : (
            <ul className="review-queue-list">
              {queue.map((item) => {
                const itemStatus = displayStatus(item);
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={item.id === doc.id ? "review-queue-item is-selected" : "review-queue-item"}
                      onClick={() => item.id !== doc.id && navigate(`/documents/${item.id}`)}
                    >
                      <span className="review-queue-name">{item.name}</span>
                      <span className="review-queue-meta">
                        <StatusChip status={itemStatus} />
                        <span className="muted">{item.assignee ?? "Unassigned"}</span>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </aside>

        <div className="pdf-pane">
          <div className="review-pane-head">
            <h2>PDF Preview</h2>
            {doc.deedType ? <span className="muted">{doc.deedType}</span> : null}
          </div>
          {pdfState === "ready" && pdfUrl ? (
            <iframe title="PDF Preview" src={pdfUrl} />
          ) : (
            <div className="pdf-placeholder">
              <span className="pdf-placeholder-mark" aria-hidden="true" />
              <strong>{pdfState === "missing" ? "PDF is not available for this deed." : "Loading PDF…"}</strong>
              <span>Preview appears here when a file is stored.</span>
            </div>
          )}
        </div>

        <div className="review-fields">
          <form id="field-form" className="field-form" onSubmit={(e) => save(e, false)}>
            <div className="review-pane-head">
              <h2>Extracted Fields</h2>
              {isFailed && <span className="field-incomplete-tag">Incomplete</span>}
            </div>
            {isFailed && <p className="muted">Incomplete fields — Retry extract to fill from OCR.</p>}

            <PartyList
              label="Grantors"
              helpKey="review.grantors"
              rows={grantors}
              readOnly={!canEdit}
              incomplete={isFailed && !grantors.some((row) => row.trim())}
              onChange={(index, value) => setParty("grantor", index, value)}
              onAdd={() => addParty("grantor")}
              onRemove={(index, value) => requestRemove("grantor", index, value)}
            />
            <PartyList
              label="Grantees"
              helpKey="review.grantees"
              rows={grantees}
              readOnly={!canEdit}
              incomplete={isFailed && !grantees.some((row) => row.trim())}
              onChange={(index, value) => setParty("grantee", index, value)}
              onAdd={() => addParty("grantee")}
              onRemove={(index, value) => requestRemove("grantee", index, value)}
            />

            <Field
              label="Document Number"
              helpKey="review.documentNumber"
              value={documentNumber}
              onChange={(v) => {
                setDocumentNumber(v);
                markDirty();
              }}
              readOnly={!canEdit}
              incomplete={isFailed && !documentNumber}
            />
            <div className="field-pair">
              <Field
                label="Volume"
                helpKey="review.volume"
                value={volume}
                onChange={(v) => {
                  setVolume(v);
                  markDirty();
                }}
                readOnly={!canEdit}
              />
              <Field
                label="Page"
                helpKey="review.page"
                value={page}
                onChange={(v) => {
                  setPage(v);
                  markDirty();
                }}
                readOnly={!canEdit}
              />
            </div>
            <label>
              <LabelWithHelp helpKey="review.deedType">Deed Type</LabelWithHelp>
              <select
                value={deedType}
                disabled={!canEdit}
                onChange={(e) => {
                  setDeedType(e.target.value);
                  markDirty();
                }}
                aria-label="Deed Type"
              >
                <option value="">None</option>
                {deedTypes.map((item) => (
                  <option key={item.id} value={item.deedType}>
                    {item.deedType}
                  </option>
                ))}
              </select>
            </label>
            <Field
              label="Instrument Date"
              value={fields.instrumentDate ?? ""}
              onChange={(v) => update("instrumentDate", v)}
              readOnly={!canEdit}
              incomplete={isFailed && !fields.instrumentDate}
            />
            <Field
              label="Consideration"
              value={fields.consideration ?? ""}
              onChange={(v) => update("consideration", v)}
              readOnly={!canEdit}
              incomplete={isFailed && !fields.consideration}
            />
            <Field
              label="PID"
              helpKey="review.pid"
              value={pid}
              onChange={(v) => {
                setPid(v);
                update("parcelId", v);
              }}
              readOnly={!canEdit}
              incomplete={isFailed && !pid}
            />

            <fieldset className="mailing-fields">
              <legend>
                <LabelWithHelp helpKey="review.mailing">Mailing</LabelWithHelp>
              </legend>
              <Field
                label="Mailing Street"
                value={mailingStreet}
                onChange={(v) => {
                  setMailingStreet(v);
                  markDirty();
                }}
                readOnly={!canEdit}
              />
              <div className="field-triple">
                <Field
                  label="Mailing City"
                  value={mailingCity}
                  onChange={(v) => {
                    setMailingCity(v);
                    markDirty();
                  }}
                  readOnly={!canEdit}
                />
                <Field
                  label="Mailing State"
                  value={mailingState}
                  onChange={(v) => {
                    setMailingState(v);
                    markDirty();
                  }}
                  readOnly={!canEdit}
                />
                <Field
                  label="Mailing ZIP"
                  value={mailingZip}
                  onChange={(v) => {
                    setMailingZip(v);
                    markDirty();
                  }}
                  readOnly={!canEdit}
                />
              </div>
            </fieldset>

            <Field label="Client" value={fields.client ?? ""} onChange={(v) => update("client", v)} readOnly={!canEdit} incomplete={isFailed && !fields.client} />
            <label>
              Notes
              <textarea
                rows={3}
                value={fields.notes ?? ""}
                onChange={(e) => update("notes", e.target.value)}
                readOnly={!canEdit}
              />
            </label>

            <section className="software-search" data-testid="software-search">
              <h3>
                <LabelWithHelp helpKey="review.softwareSearch">Software Search</LabelWithHelp>
              </h3>
              <p className="muted">Client-scoped lookup for {doc.client}. Apply a result to fill PID, owner, and mailing.</p>
              <div className="inline-form">
                <input
                  value={softwareQuery}
                  onChange={(e) => setSoftwareQuery(e.target.value)}
                  placeholder="Parcel ID or owner"
                  aria-label="Software Search"
                />
                <button className="primary" type="button" onClick={(event) => void searchSoftware(event)}>
                  Search Software
                </button>
              </div>
              {softwareSearched && softwareResults.length === 0 ? (
                <p className="muted">No Software records match those key fields for this Client.</p>
              ) : null}
              {softwareResults.length > 0 ? (
                <ul className="software-results">
                  {softwareResults.map((row) => (
                    <li key={row.softwareRecordId ?? row.parcelId}>
                      <button type="button" className="software-result" onClick={() => applySoftware(row)}>
                        <strong>{row.parcelId}</strong>
                        <span>{row.owner ?? "—"}</span>
                        <span className="muted">{(row.address ?? extraOf(row, "mailingStreet")) || "No mailing"}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              ) : null}
            </section>

            {fields.isDraft && canEdit && <p className="muted">Draft — press Save to confirm field edits.</p>}
            <div className="row-actions">
              <button
                className="primary"
                type="button"
                disabled={!canEdit}
                onClick={async () => {
                  await endpoints.retry(doc.id);
                  await load(doc.id);
                }}
              >
                Retry Extract
              </button>
              <button className="ghost" type="button" disabled={!canEdit} onClick={(e) => void save(e, true)}>
                Save Draft
              </button>
            </div>
          </form>

          <aside className="review-side">
            <section className="panel">
              <h2>Flags</h2>
              <div className="flag-list">
                {flagDefs
                  .filter((flag) => flag.isActive)
                  .map((flag) => (
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
                  <select value={teamUser} onChange={(e) => setTeamUser(e.target.value)} aria-label="Add Team Member">
                    <option value="">Add Teammate</option>
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
              <h2>Linked Documents</h2>
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
                    <option value="">Link a Deed</option>
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
              <p className="muted">Lookup by key fields or push this deed to Software. Secrets stay in Key Vault.</p>
              {doc.lastSoftwareSyncAt ? (
                <p>
                  Last {doc.lastSoftwareSyncDirection ?? "sync"}: <strong>{doc.lastSoftwareSyncStatus ?? "—"}</strong>
                  {doc.lastSoftwareSyncFailReason ? ` — ${doc.lastSoftwareSyncFailReason}` : ""}{" "}
                  <span className="muted">{new Date(doc.lastSoftwareSyncAt).toLocaleString()}</span>
                  {doc.softwareRecordId ? ` · ${doc.softwareRecordId}` : ""}
                </p>
              ) : (
                <EmptyState title="No Software Sync Yet" body="Lookup or push to record last-sync status and fail reason on this deed." />
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
                  <button className="primary" type="button" onClick={() => setPendingPush(true)}>
                    Push to Software
                  </button>
                ) : (
                  <button
                    className="nav-disabled"
                    type="button"
                    onClick={() => navigate("/denied", { state: { action: "push to Software" } })}
                  >
                    Push to Software
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
            {notify && (
              <section className="panel">
                <h2>Notify Emails</h2>
                {!notify.enabled ? (
                  <p className="muted">Admin turned notify emails off. OCR Failed and Ready mail will not send.</p>
                ) : notify.recipients.length === 0 ? (
                  <EmptyState title="No Recipients Yet" body="Assign this deed (and optionally turn on uploader notify) so Ready / OCR Failed mail has someone to send to." />
                ) : (
                  <p>
                    {notify.events.join(" and ")} mail goes to{" "}
                    {notify.recipients.map((r) => `${r.displayName} (${r.reason})`).join(", ")}.
                  </p>
                )}
              </section>
            )}
          </aside>
        </div>
      </div>
      {saved && !isFailed && <span className="chip chip-saved saved-pill">Saved</span>}
      {pendingPush && (
        <ConfirmSheet
          title={clientConfig?.hasAnyReset ? "Push Will Reset Software Properties" : "Push to Software?"}
          body={
            clientConfig?.hasAnyReset
              ? "This Client is set to reset one or more Software property groups (exemptions, supplement year, sales letter, Sales Tab, agents, or mortgage codes). Continue?"
              : "Push this deed’s fields to Software for this Client?"
          }
          confirmLabel={clientConfig?.hasAnyReset ? "Push and Reset" : "Push"}
          danger={Boolean(clientConfig?.hasAnyReset)}
          onCancel={() => setPendingPush(false)}
          onConfirm={async () => {
            setPendingPush(false);
            await runSoftwarePush();
          }}
        />
      )}
      {pendingRemove && (
        <ConfirmSheet
          title={pendingRemove.kind === "grantor" ? "Remove Grantor?" : "Remove Grantee?"}
          body="This row has a name. Remove it from the list?"
          confirmLabel="Remove"
          onCancel={() => setPendingRemove(null)}
          onConfirm={() => {
            removeParty(pendingRemove.kind, pendingRemove.index);
            setPendingRemove(null);
          }}
        />
      )}
    </section>
  );
}

function PartyList({
  label,
  helpKey,
  rows,
  readOnly,
  incomplete,
  onChange,
  onAdd,
  onRemove
}: {
  label: string;
  helpKey: "review.grantors" | "review.grantees";
  rows: string[];
  readOnly: boolean;
  incomplete?: boolean;
  onChange: (index: number, value: string) => void;
  onAdd: () => void;
  onRemove: (index: number, value: string) => void;
}) {
  const kind = label === "Grantors" ? "Grantor" : "Grantee";
  return (
    <fieldset className="party-list" data-testid={label === "Grantors" ? "party-grantors" : "party-grantees"}>
      <legend>
        <span className="field-label-text">
          <LabelWithHelp helpKey={helpKey}>{label}</LabelWithHelp>
          {incomplete && <span className="field-incomplete-tag">Incomplete</span>}
        </span>
      </legend>
      {rows.map((row, index) => (
        <div key={`${kind}-${index}`} className="party-row">
          <input
            value={row}
            onChange={(e) => onChange(index, e.target.value)}
            readOnly={readOnly}
            aria-label={`${kind} ${index + 1}`}
            className={incomplete ? "is-incomplete" : undefined}
            aria-invalid={incomplete || undefined}
          />
          <button
            className="ghost"
            type="button"
            disabled={readOnly || (rows.length === 1 && !row.trim())}
            onClick={() => onRemove(index, row)}
            aria-label={`Remove ${kind} ${index + 1}`}
          >
            Remove
          </button>
        </div>
      ))}
      {readOnly ? null : (
        <button className="ghost" type="button" onClick={onAdd} aria-label={`Add ${kind}`}>
          Add {kind}
        </button>
      )}
    </fieldset>
  );
}

function Field({
  label,
  helpKey,
  value,
  onChange,
  readOnly,
  incomplete
}: {
  label: string;
  helpKey?: "review.documentNumber" | "review.volume" | "review.page" | "review.pid";
  value: string;
  onChange: (value: string) => void;
  readOnly: boolean;
  incomplete?: boolean;
}) {
  return (
    <label>
      <span className="field-label-text">
        {helpKey ? <LabelWithHelp helpKey={helpKey}>{label}</LabelWithHelp> : label}
        {incomplete && <span className="field-incomplete-tag">Incomplete</span>}
      </span>
      <input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        className={incomplete ? "is-incomplete" : undefined}
        aria-invalid={incomplete || undefined}
      />
    </label>
  );
}
