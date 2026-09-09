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

export function catalogLabel(status: string, statuses: StatusItem[]): string {
  if (!status) {
    return status;
  }
  if (status === "Approved") {
    const complete = statuses.find((item) => item.code === "Complete");
    if (complete) {
      return complete.displayName;
    }
  }
  const byCode = statuses.find((item) => item.code === status);
  if (byCode) {
    return byCode.displayName;
  }
  const seed = statuses.find((item) => item.isSeed && item.mapsTo === status);
  if (seed) {
    return seed.displayName;
  }
  const mapped = statuses.find((item) => item.mapsTo === status);
  if (mapped) {
    return mapped.displayName;
  }
  if (status === "NeedsReview") {
    return "Needs Work";
  }
  return status;
}

export function catalogColor(status: string, statuses: StatusItem[]): string | undefined {
  const byCode = statuses.find((item) => item.code === status);
  if (byCode) {
    return byCode.color;
  }
  const seed = statuses.find((item) => item.isSeed && item.mapsTo === status);
  return seed?.color ?? statuses.find((item) => item.mapsTo === status)?.color;
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
