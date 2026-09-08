import { FormEvent, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { endpoints, type DocumentDetail, type FieldDraft } from "../api";
import { useAuth } from "../auth";
import StatusChip from "../components/StatusChip";

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
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);

  async function load(documentId: string) {
    const detail = await endpoints.document(documentId);
    setDoc(detail);
    setFields({ ...emptyFields, ...detail.fields, isDraft: detail.fields.isDraft });
    setSaved(!detail.fields.isDraft && Boolean(detail.fields.grantor));
  }

  useEffect(() => {
    if (id) {
      load(id).catch((err) => setError(err instanceof Error ? err.message : "Could not load deed."));
    }
  }, [id]);

  useEffect(() => {
    if (!id) return;
    let objectUrl: string | undefined;
    const token = sessionStorage.getItem("deedai.token");
    fetch(`/api/documents/${id}/file`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
      .then(async (response) => {
        if (!response.ok) return;
        objectUrl = URL.createObjectURL(await response.blob());
        setPdfUrl(objectUrl);
      })
      .catch(() => setPdfUrl(null));
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      setPdfUrl(null);
    };
  }, [id]);

  function update<K extends keyof FieldDraft>(key: K, value: FieldDraft[K]) {
    setFields((current) => ({ ...current, [key]: value, isDraft: true }));
    setSaved(false);
  }

  async function save(event: FormEvent, asDraft: boolean) {
    event.preventDefault();
    if (!id) return;
    if (!canEdit) {
      navigate("/denied", { state: { action: "edit deed fields" } });
      return;
    }
    const next = await endpoints.saveFields(id, { ...fields, isDraft: asDraft });
    setFields(next);
    setSaved(!next.isDraft);
  }

  if (!doc) {
    return (
      <section className="page">
        {error ? <div className="denied-box">{error}</div> : <p>Loading…</p>}
      </section>
    );
  }

  return (
    <section className="page">
      <div className="review-header">
        <div className="title-row">
          <h1>Deed review</h1>
          <StatusChip status={doc.status} />
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

      <div className="review-grid">
        <div className="pdf-pane">
            {pdfUrl ? (
              <iframe title="PDF preview" src={pdfUrl} />
            ) : (
              <div className="pdf-placeholder">PDF preview</div>
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
      {saved && <span className="chip chip-ready saved-pill">Saved</span>}
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
