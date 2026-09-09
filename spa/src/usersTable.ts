import type { ClientItem, Role, UserDetail } from "./api";

export const USERS_TABLE_STORAGE_KEY = "deedai.users.table";
export const USERS_PAGE_SIZE = 50;

export const USERS_SORT_KEYS = ["displayName", "email", "role", "clients", "status"] as const;
export type UsersSortKey = (typeof USERS_SORT_KEYS)[number];
export type UsersSortDir = "asc" | "desc";
export type UsersStatusFilter = "" | "active" | "disabled";

export type UsersTableQuery = {
  q: string;
  sort: UsersSortKey;
  dir: UsersSortDir;
  role: string;
  client: string;
  status: UsersStatusFilter;
  page: number;
};

export const defaultUsersTableQuery: UsersTableQuery = {
  q: "",
  sort: "displayName",
  dir: "asc",
  role: "",
  client: "",
  status: "",
  page: 1
};

const USERS_PARAM_KEYS = ["q", "sort", "dir", "role", "client", "status", "page"] as const;

export function hasUsersTableParams(params: URLSearchParams) {
  return USERS_PARAM_KEYS.some((key) => params.has(key));
}

export function parseUsersTableQuery(params: URLSearchParams): UsersTableQuery {
  const sort = params.get("sort");
  const dir = params.get("dir");
  const status = params.get("status");
  const page = Number(params.get("page") ?? "1");
  return {
    q: params.get("q") ?? "",
    sort: isSortKey(sort) ? sort : defaultUsersTableQuery.sort,
    dir: dir === "desc" ? "desc" : "asc",
    role: params.get("role") ?? "",
    client: params.get("client") ?? "",
    status: status === "active" || status === "disabled" ? status : "",
    page: Number.isFinite(page) && page > 0 ? Math.floor(page) : 1
  };
}

export function serializeUsersTableQuery(query: UsersTableQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.q) params.set("q", query.q);
  if (query.sort !== defaultUsersTableQuery.sort) params.set("sort", query.sort);
  if (query.dir !== defaultUsersTableQuery.dir) params.set("dir", query.dir);
  if (query.role) params.set("role", query.role);
  if (query.client) params.set("client", query.client);
  if (query.status) params.set("status", query.status);
  if (query.page > 1) params.set("page", String(query.page));
  return params;
}

export function patchUsersTableQuery(current: UsersTableQuery, partial: Partial<UsersTableQuery>): UsersTableQuery {
  const resetsPage =
    partial.q !== undefined
    || partial.sort !== undefined
    || partial.dir !== undefined
    || partial.role !== undefined
    || partial.client !== undefined
    || partial.status !== undefined;
  return {
    ...current,
    ...partial,
    page: partial.page ?? (resetsPage ? 1 : current.page)
  };
}

export function nextUsersSort(current: UsersTableQuery, key: UsersSortKey): UsersTableQuery {
  if (current.sort === key) {
    return patchUsersTableQuery(current, { dir: current.dir === "asc" ? "desc" : "asc" });
  }
  return patchUsersTableQuery(current, { sort: key, dir: "asc" });
}

export function readStoredUsersTableQuery(): UsersTableQuery | null {
  try {
    const raw = sessionStorage.getItem(USERS_TABLE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<UsersTableQuery>;
    return {
      ...defaultUsersTableQuery,
      q: typeof parsed.q === "string" ? parsed.q : "",
      sort: isSortKey(parsed.sort) ? parsed.sort : defaultUsersTableQuery.sort,
      dir: parsed.dir === "desc" ? "desc" : "asc",
      role: typeof parsed.role === "string" ? parsed.role : "",
      client: typeof parsed.client === "string" ? parsed.client : "",
      status: parsed.status === "active" || parsed.status === "disabled" ? parsed.status : "",
      page: typeof parsed.page === "number" && parsed.page > 0 ? Math.floor(parsed.page) : 1
    };
  } catch {
    return null;
  }
}

export function writeStoredUsersTableQuery(query: UsersTableQuery) {
  sessionStorage.setItem(USERS_TABLE_STORAGE_KEY, JSON.stringify(query));
}

export function hasActiveUsersTableState(query: UsersTableQuery) {
  return (
    query.q !== ""
    || query.sort !== defaultUsersTableQuery.sort
    || query.dir !== defaultUsersTableQuery.dir
    || query.role !== ""
    || query.client !== ""
    || query.status !== ""
    || query.page > 1
  );
}

export function clientNames(user: UserDetail, clients: ClientItem[]): string[] {
  return user.clientIds
    .map((id) => clients.find((client) => client.id === id)?.name ?? id)
    .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
}

/** Client column shows every assignment (Phase 4.5 multi-Client carry). */
export function clientColumnLabel(user: UserDetail, clients: ClientItem[]): string {
  const names = clientNames(user, clients);
  return names.length ? names.join(", ") : "—";
}

export function matchesUsersSearch(user: UserDetail, q: string) {
  const needle = q.trim().toLowerCase();
  if (!needle) return true;
  const haystack = `${user.displayName} ${user.fullName ?? ""} ${user.email}`.toLowerCase();
  return haystack.includes(needle);
}

export function applyUsersTable(
  users: UserDetail[],
  clients: ClientItem[],
  query: UsersTableQuery
) {
  const filtered = users.filter((user) => {
    if (!matchesUsersSearch(user, query.q)) return false;
    if (query.role && user.role !== query.role) return false;
    if (query.client && !user.clientIds.includes(query.client)) return false;
    if (query.status === "active" && !user.isActive) return false;
    if (query.status === "disabled" && user.isActive) return false;
    return true;
  });

  const sorted = [...filtered].sort((a, b) => compareUsers(a, b, clients, query.sort, query.dir));
  const total = sorted.length;
  const totalPages = Math.max(1, Math.ceil(total / USERS_PAGE_SIZE));
  const page = Math.min(query.page, totalPages);
  const start = (page - 1) * USERS_PAGE_SIZE;
  return {
    rows: sorted.slice(start, start + USERS_PAGE_SIZE),
    total,
    page,
    totalPages
  };
}

function compareUsers(
  a: UserDetail,
  b: UserDetail,
  clients: ClientItem[],
  sort: UsersSortKey,
  dir: UsersSortDir
) {
  const left = sortValue(a, clients, sort);
  const right = sortValue(b, clients, sort);
  const n = left.localeCompare(right, undefined, { sensitivity: "base", numeric: true });
  return dir === "asc" ? n : -n;
}

function sortValue(user: UserDetail, clients: ClientItem[], sort: UsersSortKey): string {
  if (sort === "displayName") return user.displayName;
  if (sort === "email") return user.email;
  if (sort === "role") return user.role;
  if (sort === "clients") return clientNames(user, clients).join(", ");
  return user.isActive ? "Active" : "Disabled";
}

function isSortKey(value: string | null | undefined): value is UsersSortKey {
  return USERS_SORT_KEYS.includes(value as UsersSortKey);
}

export const usersRoles: Role[] = ["Admin", "Editor", "Uploader", "Viewer"];
