export type Role = "Admin" | "Editor" | "Uploader" | "Viewer";

export interface Me {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  clientIds: string[];
}

export interface LoginResponse {
  token: string;
  email: string;
  displayName: string;
  role: Role;
}

export interface ClientItem {
  id: string;
  name: string;
}

export interface UserSummary {
  id: string;
  displayName: string;
  role: Role;
}

export interface UserDetail {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
  clientIds: string[];
}

export interface FlagSummary {
  id: string;
  name: string;
  color: string;
}

export interface FlagItem extends FlagSummary {
  sortOrder: number;
  isActive: boolean;
}

export interface StatusItem {
  id: string;
  code: string;
  displayName: string;
  color: string;
  isSystem: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface DeedTypeItem {
  id: string;
  deedType: string;
  softwareCode: string;
  fieldMapJson: string | null;
  isActive: boolean;
}

export interface DocumentListItem {
  id: string;
  name: string;
  client: string;
  clientId: string;
  status: string;
  updatedAt: string;
  assignee: string | null;
  assigneeUserId: string | null;
  canRetry: boolean;
  isDeleted: boolean;
  deedType: string | null;
  reviewStatus: string | null;
  flags: FlagSummary[];
}

export interface FieldDraft {
  grantor: string | null;
  grantee: string | null;
  instrumentDate: string | null;
  consideration: string | null;
  parcelId: string | null;
  client: string | null;
  notes: string | null;
  isDraft: boolean;
}

export interface TeamMember {
  id: string;
  displayName: string;
  role: Role;
}

export interface LinkedDocument {
  id: string;
  name: string;
  note: string | null;
}

export interface DocumentDetail {
  id: string;
  name: string;
  client: string;
  clientId: string;
  status: string;
  updatedAt: string;
  assignee: string | null;
  assigneeUserId: string | null;
  errorMessage: string | null;
  diRawBlobPath: string | null;
  deedType: string | null;
  reviewStatus: string | null;
  fields: FieldDraft;
  previousId: string | null;
  nextId: string | null;
  flags: FlagSummary[];
  team: TeamMember[];
  linkedDocuments: LinkedDocument[];
}

export interface DashboardCounts {
  uploaded: number;
  queued: number;
  processing: number;
  ready: number;
  failed: number;
}

export interface SoftwareLookup {
  parcelId: string;
  owner: string | null;
  legalDescription: string | null;
  address: string | null;
  softwareRecordId: string | null;
  extra: Record<string, string>;
}

export interface ApiError extends Error {
  status: number;
  title?: string;
  field?: string;
}

function token(): string | null {
  return sessionStorage.getItem("deedai.token");
}

async function readError(response: Response): Promise<ApiError> {
  let message = "Request failed.";
  let title = "Error";
  let field: string | undefined;
  try {
    const body = await response.json();
    message = body.message ?? body.title ?? message;
    title = body.title ?? title;
    field = typeof body.field === "string" ? body.field : undefined;
  } catch {
    if (response.status === 403) {
      message = "Access denied.";
      title = "Access denied";
    }
  }
  const error = new Error(message) as ApiError;
  error.status = response.status;
  error.title = title;
  error.field = field;
  return error;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const t = token();
  if (t) {
    headers.set("Authorization", `Bearer ${t}`);
  }
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...init, headers });
  if (!response.ok) {
    throw await readError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export async function download(path: string, fallbackName: string): Promise<void> {
  const headers = new Headers();
  const t = token();
  if (t) headers.set("Authorization", `Bearer ${t}`);
  const response = await fetch(path, { headers });
  if (!response.ok) {
    throw await readError(response);
  }
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  const disposition = response.headers.get("content-disposition");
  const match = disposition?.match(/filename="?([^"]+)"?/);
  link.href = url;
  link.download = match?.[1] ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function uploadWithProgress(
  clientId: string,
  files: File[],
  onProgress: (percent: number) => void
): Promise<{ queued: number; documents: DocumentListItem[]; errors: string[] }> {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    form.append("clientId", clientId);
    files.forEach((file) => form.append("files", file));
    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/api/uploads");
    const t = token();
    if (t) xhr.setRequestHeader("Authorization", `Bearer ${t}`);
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress(Math.round((event.loaded / event.total) * 100));
      }
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(JSON.parse(xhr.responseText));
        return;
      }
      try {
        const body = JSON.parse(xhr.responseText);
        const error = new Error(body.message ?? "Upload failed.") as ApiError;
        error.status = xhr.status;
        error.title = body.title ?? "Error";
        reject(error);
      } catch {
        const error = new Error("Upload failed.") as ApiError;
        error.status = xhr.status;
        reject(error);
      }
    };
    xhr.onerror = () => reject(new Error("Upload failed."));
    xhr.send(form);
  });
}

export const endpoints = {
  login: (email: string, password: string) =>
    api<LoginResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password })
    }),
  forgotPassword: (email: string) =>
    api<{ message: string }>("/api/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email })
    }),
  resetPassword: (token: string, password: string) =>
    api<{ message: string }>("/api/auth/reset-password", {
      method: "POST",
      body: JSON.stringify({ token, password })
    }),
  me: () => api<Me>("/api/auth/me"),
  clients: () => api<ClientItem[]>("/api/clients"),
  users: () => api<UserSummary[]>("/api/users"),
  adminUsers: () => api<UserDetail[]>("/api/admin/users"),
  createUser: (body: object) =>
    api<UserDetail>("/api/admin/users", { method: "POST", body: JSON.stringify(body) }),
  updateUser: (id: string, body: object) =>
    api<UserDetail>(`/api/admin/users/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  disableUser: (id: string) => api<{ message: string }>(`/api/admin/users/${id}`, { method: "DELETE" }),
  counts: (query: string) => api<DashboardCounts>(`/api/dashboard/counts${query}`),
  documents: (query: string) => api<DocumentListItem[]>(`/api/documents${query}`),
  document: (id: string) => api<DocumentDetail>(`/api/documents/${id}`),
  saveFields: (id: string, fields: FieldDraft & { deedType?: string | null; reviewStatus?: string | null }) =>
    api<FieldDraft>(`/api/documents/${id}/fields`, {
      method: "PUT",
      body: JSON.stringify(fields)
    }),
  retry: (id: string) => api<{ message: string }>(`/api/documents/${id}/retry`, { method: "POST" }),
  remove: (id: string) => api<{ message: string }>(`/api/documents/${id}`, { method: "DELETE" }),
  restore: (id: string) => api<{ message: string }>(`/api/documents/${id}/restore`, { method: "POST" }),
  assign: (id: string, assigneeUserId: string | null) =>
    api<{ message: string }>(`/api/documents/${id}/assignee`, {
      method: "PUT",
      body: JSON.stringify({ assigneeUserId })
    }),
  bulkAssign: (documentIds: string[], assigneeUserId: string | null) =>
    api<{ message: string }>("/api/documents/bulk-assign", {
      method: "POST",
      body: JSON.stringify({ documentIds, assigneeUserId })
    }),
  setFlags: (id: string, flagIds: string[]) =>
    api<{ message: string }>(`/api/documents/${id}/flags`, {
      method: "PUT",
      body: JSON.stringify({ flagIds })
    }),
  linkDocument: (id: string, targetDocumentId: string, note?: string) =>
    api<{ message: string }>(`/api/documents/${id}/links`, {
      method: "POST",
      body: JSON.stringify({ targetDocumentId, note })
    }),
  unlinkDocument: (id: string, targetId: string) =>
    api<{ message: string }>(`/api/documents/${id}/links/${targetId}`, { method: "DELETE" }),
  addTeam: (id: string, userId: string) =>
    api<{ message: string }>(`/api/documents/${id}/team`, {
      method: "POST",
      body: JSON.stringify({ userId })
    }),
  removeTeam: (id: string, userId: string) =>
    api<{ message: string }>(`/api/documents/${id}/team/${userId}`, { method: "DELETE" }),
  flags: () => api<FlagItem[]>("/api/settings/flags"),
  createFlag: (body: object) => api<FlagItem>("/api/settings/flags", { method: "POST", body: JSON.stringify(body) }),
  updateFlag: (id: string, body: object) =>
    api<FlagItem>(`/api/settings/flags/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteFlag: (id: string) => api<{ message: string }>(`/api/settings/flags/${id}`, { method: "DELETE" }),
  statuses: () => api<StatusItem[]>("/api/settings/statuses"),
  createStatus: (body: object) =>
    api<StatusItem>("/api/settings/statuses", { method: "POST", body: JSON.stringify(body) }),
  updateStatus: (id: string, body: object) =>
    api<StatusItem>(`/api/settings/statuses/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteStatus: (id: string) => api<{ message: string }>(`/api/settings/statuses/${id}`, { method: "DELETE" }),
  deedTypes: () => api<DeedTypeItem[]>("/api/settings/deed-types"),
  createDeedType: (body: object) =>
    api<DeedTypeItem>("/api/settings/deed-types", { method: "POST", body: JSON.stringify(body) }),
  updateDeedType: (id: string, body: object) =>
    api<DeedTypeItem>(`/api/settings/deed-types/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteDeedType: (id: string) => api<{ message: string }>(`/api/settings/deed-types/${id}`, { method: "DELETE" }),
  exportSettings: (format: string) => download(`/api/settings/export?format=${format}`, `deedai-settings.${format}`),
  reports: (query: string) => api<Record<string, unknown>[]>(`/api/reports/documents${query}`),
  exportReport: (query: string, format: string, name: string) =>
    download(`/api/reports/documents${query}${query.includes("?") ? "&" : "?"}format=${format}`, name),
  softwareLookup: (id: string) =>
    api<SoftwareLookup>(`/api/documents/${id}/software/lookup`, { method: "POST" }),
  softwarePush: (id: string) =>
    api<{ succeeded: boolean; softwareRecordId: string | null; message: string }>(
      `/api/documents/${id}/software/push`,
      { method: "POST" }
    )
};
