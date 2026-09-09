import { useId, type ReactNode } from "react";
import { HELP, type HelpKey } from "../helpCatalog";

/** Native title / visually-hidden description. No question-mark pills. */
export function FieldHelp({ helpKey }: { helpKey: HelpKey }) {
  const text = HELP[helpKey];
  const describedById = useId();
  return (
    <span className="field-help" data-help={helpKey} title={text} aria-describedby={describedById}>
      <span id={describedById} className="visually-hidden">
        {text}
      </span>
    </span>
  );
}

export function LabelWithHelp({ helpKey, children }: { helpKey: HelpKey; children: ReactNode }) {
  const text = HELP[helpKey];
  const describedById = useId();
  return (
    <span className="field-label-text" data-help={helpKey} title={text} aria-describedby={describedById}>
      {children}
      <span id={describedById} className="visually-hidden">
        {text}
      </span>
    </span>
  );
}
