/** GIS Dashboard chrome (PASS SoT). Live GIS tokens from appgisdashboard CSS :root. */
export const maskF = {
  primary: "#1890ff",
  primaryHover: "#40a9ff",
  rail: "#001529",
  railMenu: "#000c17",
  navy: "#1E2430",
  canvas: "#F0F2F5",
  surface: "#FFFFFF",
  text: "#1A1F2A",
  muted: "#5C6573",
  teal: "#0D8A7F",
  header: "#FFFFFF",
  gold: "#E8C547",
  accent: "#3B82F6",
  navActive: "#1890FF",
  maskText: "#000000d9",
  maskTextSecondary: "#00000073",
  maskBorder: "#f0f0f0",
  maskRadius: "2px",
  ready: "#D8F0EA",
  readyFg: "#0B5F56",
  failed: "#F5D6D3",
  failedFg: "#8B2E28",
  processing: "#E8C96A",
  queued: "#C5CED6",
  review: "#C5E8E4"
} as const;

export const OCR_RIBBON_STEPS = ["Upload", "Queued", "Processing", "Review", "Ready"] as const;
export type OcrRibbonStep = (typeof OCR_RIBBON_STEPS)[number];

export function ribbonStepForDocument(status?: string | null, display?: string | null): OcrRibbonStep | null {
  const shown = display || status || "";
  if (shown === "NeedsReview" || shown === "Needs review" || shown === "NeedsWork" || shown === "Needs Work") return "Review";
  if (status === "Failed") return "Review";
  if (status === "Queued") return "Queued";
  if (status === "Processing") return "Processing";
  if (status === "Ready" || shown === "Approved") return "Ready";
  return null;
}

/** Documents list ribbon highlight — pipeline stage only, never catalog labels. */
export function ribbonStepForStage(stage?: string | null): OcrRibbonStep | null {
  if (stage === "Queued") return "Queued";
  if (stage === "Processing") return "Processing";
  if (stage === "Ready") return "Ready";
  if (stage === "Review") return "Review";
  return null;
}
