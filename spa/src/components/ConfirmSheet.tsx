export default function ConfirmSheet({
  title,
  body,
  confirmLabel,
  onCancel,
  onConfirm
}: {
  title: string;
  body: string;
  confirmLabel: string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="sheet-backdrop" role="dialog" aria-modal="true" aria-labelledby="sheet-title">
      <div className="sheet">
        <h2 id="sheet-title">{title}</h2>
        <p>{body}</p>
        <div className="row-actions">
          <button className="ghost" type="button" onClick={onCancel}>
            Cancel
          </button>
          <button className="danger" type="button" onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
