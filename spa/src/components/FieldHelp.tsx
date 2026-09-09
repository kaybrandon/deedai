import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { HELP, type HelpKey } from "../helpCatalog";

export function FieldHelp({ helpKey }: { helpKey: HelpKey }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLSpanElement>(null);
  const tooltipId = useId();
  const text = HELP[helpKey];

  useEffect(() => {
    if (!open) {
      return;
    }

    const onPointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <span className="field-help" ref={rootRef} data-help={helpKey}>
      <button
        type="button"
        className="field-help-btn"
        aria-label={`Help: ${helpKey}`}
        aria-expanded={open}
        aria-controls={tooltipId}
        onClick={() => setOpen((current) => !current)}
      >
        ?
      </button>
      {open && (
        <span className="field-help-pop" id={tooltipId} role="tooltip">
          {text}
        </span>
      )}
    </span>
  );
}

export function LabelWithHelp({ helpKey, children }: { helpKey: HelpKey; children: ReactNode }) {
  return (
    <span className="field-label-text">
      {children}
      <FieldHelp helpKey={helpKey} />
    </span>
  );
}
