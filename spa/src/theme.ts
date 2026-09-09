/** Mask A — Mist Slate + Teal. Hex SoT for charts and ribbon (CSS :root is the shell SoT). */
export const maskA = {
  bg: "#F4F6F8",
  sidebar: "#E8EEF2",
  accent: "#4F7C8A",
  accentHover: "#3E6774",
  text: "#2C3A45",
  muted: "#5A6B76",
  ready: "#A8D5C0",
  failed: "#E8B4B0",
  processing: "#E8C96A",
  queued: "#C5CED6",
  review: "#C5DCE2"
} as const;

export const OCR_RIBBON_STEPS = ["Upload", "Queued", "Processing", "Review", "Ready"] as const;
export type OcrRibbonStep = (typeof OCR_RIBBON_STEPS)[number];

export function ribbonStepForDocument(status?: string | null, display?: string | null): OcrRibbonStep | null {
  const shown = display || status || "";
  if (shown === "NeedsReview" || shown === "Needs review") return "Review";
  if (status === "Failed") return "Review";
  if (status === "Queued") return "Queued";
  if (status === "Processing") return "Processing";
  if (status === "Ready" || shown === "Approved") return "Ready";
  return null;
}
