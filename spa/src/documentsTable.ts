import type { DocumentListItem } from "./api";

export const DOCUMENTS_TABLE_STORAGE_KEY = "deedai.documents.table";
export const DOCUMENTS_PAGE_SIZE = 50;

export const DOCUMENTS_SORT_KEYS = [
  "status",
  "client",
  "volume",
  "page",
  "type",
  "pid",
  "documentNumber",
  "assignee",
  "updated"
] as const;

export type DocumentsSortKey = (typeof DOCUMENTS_SORT_KEYS)[number];
export type DocumentsSortDir = "asc" | "desc";

export const DOCUMENTS_PIPELINE_STATUSES = ["Queued", "Processing", "Ready", "Failed", "NeedsReview", "Review"] as const;
export const DOCUMENTS_STAGES = ["Queued", "Processing", "Review", "Ready", "Failed"] as const;

export type DocumentsStage = (typeof DOCUMENTS_STAGES)[number] | "";

export type DocumentsTableQuery = {
  search: string;
  status: string;
  stage: DocumentsStage;
  clientId: string;
  assigneeUserId: string;
  from: string;
  to: string;
  type: string;
  includeDeleted: boolean;
  sort: DocumentsSortKey;
  dir: DocumentsSortDir;
  page: number;
};

export const defaultDocumentsTableQuery: DocumentsTableQuery = {
  search: "",
  status: "",
  stage: "",
  clientId: "",
  assigneeUserId: "",
  from: "",
  to: "",
  type: "",
  includeDeleted: false,
  sort: "updated",
  dir: "desc",
  page: 1
};

const DOCUMENTS_PARAM_KEYS = [
  "search",
  "status",
  "stage",
  "clientId",
  "assigneeUserId",
  "from",
  "to",
  "type",
  "includeDeleted",
  "sort",
  "dir",
  "page"
] as const;

export function isDocumentsPipelineStatus(value: string): boolean {
  return DOCUMENTS_PIPELINE_STATUSES.some((item) => item.toLowerCase() === value.toLowerCase());
}

export function normalizeDocumentsStage(value: string | null | undefined): DocumentsStage {
  if (!value) return "";
  const trimmed = value.trim();
  if (/^queued$/i.test(trimmed)) return "Queued";
  if (/^processing$/i.test(trimmed)) return "Processing";
  if (/^ready$/i.test(trimmed)) return "Ready";
  if (/^failed$/i.test(trimmed)) return "Failed";
  if (/^review$/i.test(trimmed) || /^needsreview$/i.test(trimmed)) return "Review";
  return "";
}

export function splitDocumentsStatusAndStage(status: string, stage: string): { status: string; stage: DocumentsStage } {
  const normalizedStage = normalizeDocumentsStage(stage);
  if (normalizedStage) {
    return {
      stage: normalizedStage,
      status: isDocumentsPipelineStatus(status) ? "" : status
    };
  }
  if (isDocumentsPipelineStatus(status)) {
    return { stage: normalizeDocumentsStage(status), status: "" };
  }
  return { stage: "", status };
}

export function stageToApiStatus(stage: DocumentsStage): string {
  if (stage === "Review") return "NeedsReview";
  return stage;
}

export function matchesDocumentsStage(row: DocumentListItem, stage: DocumentsStage): boolean {
  if (!stage) return true;
  if (stage === "Queued") return row.status === "Queued";
  if (stage === "Processing") return row.status === "Processing";
  if (stage === "Ready") return row.status === "Ready";
  if (stage === "Failed") return row.status === "Failed";
  const shown = `${row.displayStatus ?? ""} ${row.reviewStatus ?? ""}`;
  return (
    row.status === "Failed"
    || /needsreview|needs work|needswork/i.test(shown)
  );
}

export function dateInput(value: string | null) {
  if (!value) {
    return "";
  }
  return value.length >= 10 ? value.slice(0, 10) : value;
}

export function hasDocumentsTableParams(params: URLSearchParams) {
  return DOCUMENTS_PARAM_KEYS.some((key) => params.has(key));
}

export function parseDocumentsTableQuery(params: URLSearchParams): DocumentsTableQuery {
  const sort = params.get("sort");
  const dir = params.get("dir");
  const page = Number(params.get("page") ?? "1");
  const split = splitDocumentsStatusAndStage(params.get("status") ?? "", params.get("stage") ?? "");
  return {
    search: params.get("search") ?? "",
    status: split.status,
    stage: split.stage,
    clientId: params.get("clientId") ?? "",
    assigneeUserId: params.get("assigneeUserId") ?? "",
    from: dateInput(params.get("from")),
    to: dateInput(params.get("to")),
    type: params.get("type") ?? "",
    includeDeleted: params.get("includeDeleted") === "true",
    sort: isSortKey(sort) ? sort : defaultDocumentsTableQuery.sort,
    dir: dir === "asc" ? "asc" : dir === "desc" ? "desc" : defaultDocumentsTableQuery.dir,
    page: Number.isFinite(page) && page > 0 ? Math.floor(page) : 1
  };
}

export function serializeDocumentsTableQuery(query: DocumentsTableQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.stage) params.set("stage", query.stage);
  if (query.clientId) params.set("clientId", query.clientId);
  if (query.assigneeUserId) params.set("assigneeUserId", query.assigneeUserId);
  if (query.from) params.set("from", query.from);
  if (query.to) params.set("to", query.to);
  if (query.type) params.set("type", query.type);
  if (query.includeDeleted) params.set("includeDeleted", "true");
  if (query.sort !== defaultDocumentsTableQuery.sort) params.set("sort", query.sort);
  if (query.dir !== defaultDocumentsTableQuery.dir) params.set("dir", query.dir);
  if (query.page > 1) params.set("page", String(query.page));
  return params;
}

export function documentsApiQuery(query: DocumentsTableQuery): string {
  const params = new URLSearchParams();
  const status = query.status || stageToApiStatus(query.stage);
  if (status) params.set("status", status);
  if (query.clientId) params.set("clientId", query.clientId);
  if (query.assigneeUserId) params.set("assigneeUserId", query.assigneeUserId);
  if (query.from) params.set("from", new Date(query.from).toISOString());
  if (query.to) params.set("to", new Date(`${query.to}T23:59:59`).toISOString());
  if (query.includeDeleted) params.set("includeDeleted", "true");
  if (query.type) params.set("deedType", query.type);
  const text = params.toString();
  return text ? `?${text}` : "";
}

export function patchDocumentsTableQuery(
  current: DocumentsTableQuery,
  partial: Partial<DocumentsTableQuery>
): DocumentsTableQuery {
  const resetsPage =
    partial.search !== undefined
    || partial.status !== undefined
    || partial.stage !== undefined
    || partial.clientId !== undefined
    || partial.assigneeUserId !== undefined
    || partial.from !== undefined
    || partial.to !== undefined
    || partial.type !== undefined
    || partial.includeDeleted !== undefined
    || partial.sort !== undefined
    || partial.dir !== undefined;
  return {
    ...current,
    ...partial,
    page: partial.page ?? (resetsPage ? 1 : current.page)
  };
}

export function nextDocumentsSort(current: DocumentsTableQuery, key: DocumentsSortKey): DocumentsTableQuery {
  if (current.sort === key) {
    return patchDocumentsTableQuery(current, { dir: current.dir === "asc" ? "desc" : "asc" });
  }
  return patchDocumentsTableQuery(current, { sort: key, dir: key === "updated" ? "desc" : "asc" });
}

export function readStoredDocumentsTableQuery(): DocumentsTableQuery | null {
  try {
    const raw = sessionStorage.getItem(DOCUMENTS_TABLE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<DocumentsTableQuery>;
    const split = splitDocumentsStatusAndStage(
      typeof parsed.status === "string" ? parsed.status : "",
      typeof parsed.stage === "string" ? parsed.stage : ""
    );
    return {
      ...defaultDocumentsTableQuery,
      search: typeof parsed.search === "string" ? parsed.search : "",
      status: split.status,
      stage: split.stage,
      clientId: typeof parsed.clientId === "string" ? parsed.clientId : "",
      assigneeUserId: typeof parsed.assigneeUserId === "string" ? parsed.assigneeUserId : "",
      from: typeof parsed.from === "string" ? dateInput(parsed.from) : "",
      to: typeof parsed.to === "string" ? dateInput(parsed.to) : "",
      type: typeof parsed.type === "string" ? parsed.type : "",
      includeDeleted: parsed.includeDeleted === true,
      sort: isSortKey(parsed.sort) ? parsed.sort : defaultDocumentsTableQuery.sort,
      dir: parsed.dir === "asc" ? "asc" : parsed.dir === "desc" ? "desc" : defaultDocumentsTableQuery.dir,
      page: typeof parsed.page === "number" && parsed.page > 0 ? Math.floor(parsed.page) : 1
    };
  } catch {
    return null;
  }
}

export function writeStoredDocumentsTableQuery(query: DocumentsTableQuery) {
  sessionStorage.setItem(DOCUMENTS_TABLE_STORAGE_KEY, JSON.stringify(query));
}

export function hasActiveDocumentsTableState(query: DocumentsTableQuery) {
  return (
    query.search !== ""
    || query.status !== ""
    || query.stage !== ""
    || query.clientId !== ""
    || query.assigneeUserId !== ""
    || query.from !== ""
    || query.to !== ""
    || query.type !== ""
    || query.includeDeleted
    || query.sort !== defaultDocumentsTableQuery.sort
    || query.dir !== defaultDocumentsTableQuery.dir
    || query.page > 1
  );
}

export function hasDocumentsListFilters(query: DocumentsTableQuery) {
  return (
    query.search !== ""
    || query.status !== ""
    || query.stage !== ""
    || query.clientId !== ""
    || query.assigneeUserId !== ""
    || query.from !== ""
    || query.to !== ""
    || query.type !== ""
    || query.includeDeleted
  );
}

export function matchesDocumentsSearch(row: DocumentListItem, search: string) {
  const needle = search.trim().toLowerCase();
  if (!needle) return true;
  const haystack = [
    row.name,
    row.status,
    row.displayStatus,
    row.assignee,
    row.volume,
    row.page,
    row.documentNumber,
    row.pid,
    row.deedType,
    row.client,
    row.mailingStreet,
    row.mailingCity,
    row.mailingState,
    row.mailingZip,
    ...(row.grantors ?? []),
    ...(row.grantees ?? [])
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
  return haystack.includes(needle);
}

export function applyDocumentsTable(rows: DocumentListItem[], query: DocumentsTableQuery) {
  const filtered = rows.filter((row) => {
    if (!matchesDocumentsSearch(row, query.search)) return false;
    if (query.type && row.deedType !== query.type) return false;
    if (query.status && query.stage && !matchesDocumentsStage(row, query.stage)) return false;
    return true;
  });
  const sorted = [...filtered].sort((a, b) => compareDocuments(a, b, query.sort, query.dir));
  const total = sorted.length;
  const totalPages = Math.max(1, Math.ceil(total / DOCUMENTS_PAGE_SIZE));
  const page = Math.min(query.page, totalPages);
  const start = (page - 1) * DOCUMENTS_PAGE_SIZE;
  return {
    rows: sorted.slice(start, start + DOCUMENTS_PAGE_SIZE),
    total,
    page,
    totalPages
  };
}

function compareDocuments(a: DocumentListItem, b: DocumentListItem, sort: DocumentsSortKey, dir: DocumentsSortDir) {
  const left = sortValue(a, sort);
  const right = sortValue(b, sort);
  const n = left.localeCompare(right, undefined, { sensitivity: "base", numeric: true });
  return dir === "asc" ? n : -n;
}

function sortValue(row: DocumentListItem, sort: DocumentsSortKey): string {
  if (sort === "status") return row.displayStatus || row.status;
  if (sort === "client") return row.client;
  if (sort === "volume") return row.volume ?? "";
  if (sort === "page") return row.page ?? "";
  if (sort === "type") return row.deedType ?? "";
  if (sort === "pid") return row.pid ?? "";
  if (sort === "documentNumber") return row.documentNumber ?? "";
  if (sort === "assignee") return row.assignee ?? "";
  return row.updatedAt;
}

function isSortKey(value: string | null | undefined): value is DocumentsSortKey {
  return DOCUMENTS_SORT_KEYS.includes(value as DocumentsSortKey);
}
