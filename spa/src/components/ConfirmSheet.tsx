import { FieldHelp } from "./FieldHelp";
import type { HelpKey } from "../helpCatalog";

export default function ConfirmSheet({
  title,
  body,
  confirmLabel,
  onCancel,
  onConfirm,
  danger = true,
  helpKey
}: {
  title: string;
  body: string;
  confirmLabel: string;
  onCancel: () => void;
  onConfirm: () => void;
  danger?: boolean;
  helpKey?: HelpKey;
}) {
  return (
    <div className="sheet-backdrop" role="dialog" aria-modal="true" aria-labelledby="sheet-title">
      <div className="sheet">
        <h2 id="sheet-title">
          {title}
          {helpKey && <FieldHelp helpKey={helpKey} />}
        </h2>
        <p>{body}</p>
        <div className="row-actions">
          <button className="ghost" type="button" onClick={onCancel}>
            Cancel
          </button>
          <button className={danger ? "danger" : "primary"} type="button" onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
