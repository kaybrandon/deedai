import type { StatusItem } from "./api";

export const MUST_STATUS_LABELS = [
  "Complete",
  "In Queue",
  "Needs Work",
  "New",
  "Not Needed",
  "Pending",
  "Research",
  "Upload Error"
] as const;

const PIPELINE_TO_CATALOG: Record<string, string> = {
  Queued: "InQueue",
  Processing: "Pending",
  Ready: "Complete",
  Failed: "UploadError",
  NeedsReview: "NeedsWork",
  "Needs review": "NeedsWork",
  Approved: "Complete"
};

export function catalogCodeForStatus(status: string): string {
  return PIPELINE_TO_CATALOG[status] ?? status;
}

export function catalogLabel(status: string, statuses: StatusItem[]): string {
  if (!status) {
    return status;
  }
  const catalogCode = catalogCodeForStatus(status);
  const seed =
    statuses.find((item) => item.isSeed && (item.code === catalogCode || item.code === status || item.mapsTo === status))
    ?? statuses.find((item) => item.code === catalogCode && !item.isSystem);
  if (seed) {
    return seed.displayName;
  }
  const named = MUST_STATUS_LABELS.find((label) => label === status || label.replace(/\s+/g, "") === catalogCode);
  if (named) {
    return named;
  }
  if (catalogCode === "NeedsWork") {
    return "Needs Work";
  }
  if (catalogCode === "Complete") {
    return "Complete";
  }
  if (catalogCode === "InQueue") {
    return "In Queue";
  }
  if (catalogCode === "Pending") {
    return "Pending";
  }
  if (catalogCode === "UploadError") {
    return "Upload Error";
  }
  const byCode = statuses.find((item) => item.code === status && !item.isSystem);
  if (byCode) {
    return byCode.displayName;
  }
  return status;
}

export function catalogColor(status: string, statuses: StatusItem[]): string | undefined {
  const catalogCode = catalogCodeForStatus(status);
  const seed =
    statuses.find((item) => item.isSeed && (item.code === catalogCode || item.code === status || item.mapsTo === status))
    ?? statuses.find((item) => item.code === catalogCode && !item.isSystem);
  if (seed) {
    return seed.color;
  }
  return statuses.find((item) => item.mapsTo === status && !item.isSystem)?.color;
}

export function assignableStatuses(statuses: StatusItem[]): StatusItem[] {
  const hidden = new Set(["NeedsReview", "Approved"]);
  return statuses.filter((item) => !item.isSystem && item.isActive && !hidden.has(item.code));
}

export function filterStatuses(statuses: StatusItem[]): StatusItem[] {
  const seen = new Set<string>();
  const rows: StatusItem[] = [];
  for (const item of statuses.filter((status) => status.isActive && (status.isSeed || !status.isSystem))) {
    if (item.code === "NeedsReview" || item.code === "Approved") {
      continue;
    }
    if (seen.has(item.code)) {
      continue;
    }
    seen.add(item.code);
    rows.push(item);
  }
  return rows.sort((a, b) => a.sortOrder - b.sortOrder || a.displayName.localeCompare(b.displayName));
}

export function assignedCatalogValue(reviewStatus: string | null | undefined, statuses: StatusItem[]): string {
  const raw = reviewStatus ?? "";
  if (!raw) {
    return "";
  }
  if (statuses.some((item) => item.code === raw && !item.isSystem)) {
    return raw;
  }
  const seed = statuses.find((item) => item.isSeed && (item.mapsTo === raw || item.code === raw));
  return seed?.code ?? raw;
}

export function filterSelectValue(queryStatus: string, statuses: StatusItem[]): string {
  if (!queryStatus) {
    return "";
  }
  if (statuses.some((item) => item.code === queryStatus)) {
    return queryStatus;
  }
  const seed = statuses.find((item) => item.isSeed && item.mapsTo === queryStatus);
  return seed?.code ?? queryStatus;
}
