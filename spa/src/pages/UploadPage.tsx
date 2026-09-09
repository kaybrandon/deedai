import { DragEvent, FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, uploadWithProgress, type ClientItem } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";
import { LabelWithHelp } from "../components/FieldHelp";
import OcrRibbon from "../components/OcrRibbon";
import { useProgress } from "../progress";

interface LocalFile {
  file: File;
  progress: number;
  error?: string;
  skipped?: boolean;
}

const MAX_BYTES = 50 * 1024 * 1024;

export default function UploadPage() {
  const { canUpload } = useAuth();
  const navigate = useNavigate();
  const progress = useProgress();
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [clientId, setClientId] = useState("");
  const [items, setItems] = useState<LocalFile[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canUpload) {
      navigate("/denied", { state: { action: "upload documents" } });
      return;
    }
    endpoints.clients().then((list) => {
      setClients(list);
      if (list[0]) setClientId(list[0].id);
    });
  }, [canUpload, navigate]);

  function addFiles(files: FileList | File[]) {
    const next = Array.from(files).map((file) => {
      if (!file.name.toLowerCase().endsWith(".pdf")) {
        return { file, progress: 0, error: "PDF only." };
      }
      if (file.size > MAX_BYTES) {
        return { file, progress: 0, error: "Max 50 MB each." };
      }
      return { file, progress: 0 };
    });
    setItems((current) => [...current, ...next]);
  }

  function onDrop(event: DragEvent) {
    event.preventDefault();
    if (event.dataTransfer.files.length) {
      addFiles(event.dataTransfer.files);
    }
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const ready = items.filter((item) => !item.error && !item.skipped).map((item) => item.file);
    if (!clientId) {
      setError("Select a Client.");
      return;
    }
    if (ready.length === 0) {
      setError("Add at least one PDF.");
      return;
    }

    const jobId = `upload-${Date.now()}`;
    progress.start(jobId, `Uploading ${ready.length} PDF${ready.length === 1 ? "" : "s"}`);
    try {
      const result = await uploadWithProgress(clientId, ready, (percent) => {
        progress.update(jobId, percent);
        setItems((current) =>
          current.map((item) => (item.error || item.skipped ? item : { ...item, progress: percent }))
        );
      });
      progress.finish(jobId);
      setItems((current) =>
        current.map((item) => {
          if (item.error || item.skipped) return item;
          const failed = result.errors.find((msg) => msg.startsWith(item.file.name));
          return failed ? { ...item, progress: 0, error: failed } : { ...item, progress: 100 };
        })
      );
      setNotice(`${result.queued} queued for OCR`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Upload failed.";
      progress.finish(jobId, message);
      setError(message);
    }
  }

  return (
    <section className="page has-ocr-ribbon">
      <OcrRibbon current="Upload" />
      <h1>Upload documents</h1>
      <p className="muted">Upload continues in the progress dock if you leave this page.</p>
      <form onSubmit={onSubmit}>
        <label className="narrow">
          <LabelWithHelp helpKey="upload.client">Client</LabelWithHelp>
          <select value={clientId} onChange={(e) => setClientId(e.target.value)}>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.name}
              </option>
            ))}
          </select>
        </label>
        <div
          className="dropzone"
          onDragOver={(e) => e.preventDefault()}
          onDrop={onDrop}
          onClick={() => document.getElementById("file-input")?.click()}
        >
          <strong>Drag and drop PDF files here</strong>
          <span>PDF only. Max 50 MB each.</span>
          <input
            id="file-input"
            type="file"
            accept="application/pdf,.pdf"
            multiple
            hidden
            onChange={(e) => e.target.files && addFiles(e.target.files)}
          />
        </div>
        {items.length === 0 ? (
          <EmptyState title="No files queued" body="Drop PDFs here. Progress will not block the rest of Deed AI." />
        ) : (
          <ul className="file-list">
            {items.map((item, index) => (
              <li key={`${item.file.name}-${index}`}>
                {item.error ? (
                  <span className="fail-line">
                    {item.file.name} failed —{" "}
                    <button
                      className="link"
                      type="button"
                      onClick={() =>
                        setItems((current) =>
                          current.map((row, i) => (i === index ? { file: row.file, progress: 0 } : row))
                        )
                      }
                    >
                      Retry
                    </button>{" "}
                    |{" "}
                    <button
                      className="link"
                      type="button"
                      onClick={() =>
                        setItems((current) =>
                          current.map((row, i) => (i === index ? { ...row, skipped: true } : row))
                        )
                      }
                    >
                      Skip
                    </button>
                  </span>
                ) : item.skipped ? (
                  <span className="muted">{item.file.name} skipped</span>
                ) : (
                  <>
                    <span>{item.file.name}</span>
                    <progress max={100} value={item.progress} />
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
        <div className="row-actions">
          <button className="primary" type="submit">
            Upload
          </button>
          <button
            className="ghost"
            type="button"
            onClick={() => {
              setItems([]);
              setNotice(null);
            }}
          >
            Clear
          </button>
        </div>
      </form>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
    </section>
  );
}
