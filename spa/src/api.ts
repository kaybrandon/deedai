export type Role = "Admin" | "Editor" | "Uploader" | "Viewer";

export interface Me {
  id: string;
  email: string;
  displayName: string;
  role: Role;
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
  fields: FieldDraft;
  previousId: string | null;
  nextId: string | null;
}

export interface DashboardCounts {
  uploaded: number;
  queued: number;
  processing: number;
  ready: number;
  failed: number;
}

export interface ApiError extends Error {
  status: number;
  title?: string;
}

function token(): string | null {
  return sessionStorage.getItem("deedai.token");
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
    let message = "Request failed.";
    let title = "Error";
    try {
      const body = await response.json();
      message = body.message ?? body.title ?? message;
      title = body.title ?? title;
    } catch {
      if (response.status === 403) {
        message = "Access denied.";
        title = "Access denied";
      }
    }
    const error = new Error(message) as ApiError;
    error.status = response.status;
    error.title = title;
    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export const endpoints = {
  login: (email: string, password: string) =>
    api<LoginResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password })
    }),
  me: () => api<Me>("/api/auth/me"),
  clients: () => api<ClientItem[]>("/api/clients"),
  users: () => api<UserSummary[]>("/api/users"),
  counts: (query: string) => api<DashboardCounts>(`/api/dashboard/counts${query}`),
  documents: (query: string) => api<DocumentListItem[]>(`/api/documents${query}`),
  document: (id: string) => api<DocumentDetail>(`/api/documents/${id}`),
  saveFields: (id: string, fields: FieldDraft) =>
    api<FieldDraft>(`/api/documents/${id}/fields`, {
      method: "PUT",
      body: JSON.stringify(fields)
    }),
  retry: (id: string) => api<{ message: string }>(`/api/documents/${id}/retry`, { method: "POST" }),
  remove: (id: string) => api<{ message: string }>(`/api/documents/${id}`, { method: "DELETE" }),
  restore: (id: string) => api<{ message: string }>(`/api/documents/${id}/restore`, { method: "POST" }),
  upload: (clientId: string, files: File[]) => {
    const form = new FormData();
    form.append("clientId", clientId);
    files.forEach((file) => form.append("files", file));
    return api<{ queued: number; documents: DocumentListItem[]; errors: string[] }>("/api/uploads", {
      method: "POST",
      body: form
    });
  }
};
