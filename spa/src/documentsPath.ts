export type DocumentsPathFilters = {
  status?: string;
  clientId?: string;
  assigneeUserId?: string | null;
};

/** Same Documents query params the dashboard status cards already use, plus assignee. */
export function documentsPath(filters: DocumentsPathFilters = {}): string {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.clientId) params.set("clientId", filters.clientId);
  if (filters.assigneeUserId) params.set("assigneeUserId", filters.assigneeUserId);
  const query = params.toString();
  return query ? `/documents?${query}` : "/documents";
}
