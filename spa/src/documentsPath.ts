export type DocumentsPathFilters = {
  status?: string;
  clientId?: string;
  assigneeUserId?: string | null;
  from?: string;
  to?: string;
};

/** Documents query params used by dashboard cards and charts (status, user, Client, dates). */
export function documentsPath(filters: DocumentsPathFilters = {}): string {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.clientId) params.set("clientId", filters.clientId);
  if (filters.assigneeUserId) params.set("assigneeUserId", filters.assigneeUserId);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  const query = params.toString();
  return query ? `/documents?${query}` : "/documents";
}
