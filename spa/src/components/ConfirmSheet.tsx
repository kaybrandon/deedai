import { useId } from "react";
import { HELP, type HelpKey } from "../helpCatalog";

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
  const helpId = useId();
  const help = helpKey ? HELP[helpKey] : undefined;
  return (
    <div
      className="sheet-backdrop"
      role="dialog"
      aria-modal="true"
      aria-labelledby="sheet-title"
      aria-describedby={help ? helpId : undefined}
    >
      <div className="sheet">
        <h2 id="sheet-title" title={help} data-help={helpKey}>
          {title}
        </h2>
        {help && (
          <p id={helpId} className="visually-hidden">
            {help}
          </p>
        )}
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
