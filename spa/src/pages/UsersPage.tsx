import { FormEvent, useEffect, useMemo, useState, type ReactNode } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { endpoints, type ApiError, type ClientItem, type Role, type UserDetail } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import { LabelWithHelp } from "../components/FieldHelp";
import PasswordPair, { passwordPairErrors } from "../components/PasswordPair";
import PhotoEditor from "../components/PhotoEditor";
import UserAvatar from "../components/UserAvatar";
import {
  applyUsersTable,
  clientColumnLabel,
  hasActiveUsersTableState,
  hasUsersTableParams,
  nextUsersSort,
  parseUsersTableQuery,
  patchUsersTableQuery,
  readStoredUsersTableQuery,
  serializeUsersTableQuery,
  usersRoles,
  writeStoredUsersTableQuery,
  type UsersSortKey,
  type UsersTableQuery
} from "../usersTable";

const blank = {
  email: "",
  displayName: "",
  fullName: "",
  role: "Viewer" as Role,
  password: "",
  confirm: "",
  isActive: true,
  clientIds: [] as string[]
};

export default function UsersPage() {
  const { canAdmin } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseUsersTableQuery(searchParams), [searchParams]);
  const [users, setUsers] = useState<UserDetail[]>([]);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const [form, setForm] = useState(blank);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [photoRevision, setPhotoRevision] = useState(0);
  const [pendingDisable, setPendingDisable] = useState<UserDetail | null>(null);
  const [pendingResend, setPendingResend] = useState<UserDetail | null>(null);

  async function load() {
    setUsers(await endpoints.adminUsers());
    setClients(await endpoints.settingsClients());
  }

  function applyQuery(next: UsersTableQuery) {
    setSearchParams(serializeUsersTableQuery(next), { replace: true });
    writeStoredUsersTableQuery(next);
  }

  function patchQuery(partial: Partial<UsersTableQuery>) {
    applyQuery(patchUsersTableQuery(query, partial));
  }

  useEffect(() => {
    if (!canAdmin) {
      navigate("/denied", { state: { action: "manage users" } });
      return;
    }
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load users."));
  }, [canAdmin, navigate]);

  useEffect(() => {
    if (!canAdmin) return;
    if (hasUsersTableParams(searchParams)) {
      writeStoredUsersTableQuery(parseUsersTableQuery(searchParams));
      return;
    }
    const stored = readStoredUsersTableQuery();
    if (stored && hasActiveUsersTableState(stored)) {
      setSearchParams(serializeUsersTableQuery(stored), { replace: true });
    }
  }, [canAdmin, searchParams, setSearchParams]);

  const table = useMemo(() => applyUsersTable(users, clients, query), [users, clients, query]);

  function startCreate() {
    setEditing("new");
    setPasswordError(null);
    setConfirmError(null);
    setPhotoError(null);
    setForm({ ...blank, clientIds: clients.map((c) => c.id) });
  }

  function startEdit(user: UserDetail) {
    setEditing(user.id);
    setPasswordError(null);
    setConfirmError(null);
    setPhotoError(null);
    setForm({
      email: user.email,
      displayName: user.displayName,
      fullName: user.fullName ?? "",
      role: user.role,
      password: "",
      confirm: "",
      isActive: user.isActive,
      clientIds: user.clientIds
    });
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const next = passwordPairErrors(form.password, form.confirm, editing === "new");
    if (next.password || next.confirm) {
      setPasswordError(next.password);
      setConfirmError(next.confirm);
      return;
    }
    setPasswordError(null);
    setConfirmError(null);
    const body = {
      email: form.email,
      displayName: form.displayName,
      fullName: form.fullName,
      role: form.role,
      password: form.password || null,
      isActive: form.isActive,
      clientIds: form.clientIds
    };
    try {
      if (editing === "new") {
        await endpoints.createUser(body);
        setNotice("User created.");
      } else if (editing) {
        await endpoints.updateUser(editing, body);
        setNotice("User updated.");
      }
      setEditing(null);
      await load();
    } catch (err) {
      const apiError = err as ApiError;
      if (apiError.field === "password") {
        setPasswordError(apiError.message);
      } else {
        setError(apiError.message ?? "Save failed.");
      }
    }
  }

  function toggleClient(id: string) {
    setForm((current) => ({
      ...current,
      clientIds: current.clientIds.includes(id)
        ? current.clientIds.filter((item) => item !== id)
        : [...current.clientIds, id]
    }));
  }

  const editingUser = editing && editing !== "new" ? users.find((user) => user.id === editing) : null;

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <h1>Users</h1>
          <p className="page-kicker">Roles and Client access. Search, sort, and filter the Users table. Multi-Client users list every assignment in Client(s).</p>
        </div>
        <button className="primary" type="button" onClick={startCreate}>
          Add User
        </button>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      <div className="filter-row users-filter-row">
        <input
          className="users-search"
          type="search"
          aria-label="Search users"
          placeholder="Search name or email"
          value={query.q}
          onChange={(e) => patchQuery({ q: e.target.value })}
        />
      </div>
      {users.length === 0 ? (
        <EmptyState title="No Users Yet" body="Admins can create accounts and map Client access." />
      ) : table.total === 0 ? (
        <EmptyState title="No Users Match" body="Try another name, email, Role, Client, or Status." />
      ) : (
        <>
          <div className="table-wrap users-table-wrap">
            <table className="users-table" data-table="users" aria-label="Users">
              <thead>
                <tr>
                  <SortFilterTh label="Display Name" sortKey="displayName" query={query} onSort={(key) => applyQuery(nextUsersSort(query, key))} />
                  <th>Full Name</th>
                  <SortFilterTh label="Email" sortKey="email" query={query} onSort={(key) => applyQuery(nextUsersSort(query, key))} />
                  <SortFilterTh
                    label="Role"
                    sortKey="role"
                    query={query}
                    onSort={(key) => applyQuery(nextUsersSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Role"
                        value={query.role}
                        onChange={(e) => patchQuery({ role: e.target.value })}
                      >
                        <option value="">All Roles</option>
                        {usersRoles.map((role) => (
                          <option key={role} value={role}>
                            {role}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh
                    label="Client(s)"
                    sortKey="clients"
                    query={query}
                    onSort={(key) => applyQuery(nextUsersSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Client"
                        value={query.client}
                        onChange={(e) => patchQuery({ client: e.target.value })}
                      >
                        <option value="">All Clients</option>
                        {clients.map((client) => (
                          <option key={client.id} value={client.id}>
                            {client.name}
                          </option>
                        ))}
                      </select>
                    }
                  />
                  <SortFilterTh
                    label="Status"
                    sortKey="status"
                    query={query}
                    onSort={(key) => applyQuery(nextUsersSort(query, key))}
                    filter={
                      <select
                        className="th-filter"
                        aria-label="Filter Status"
                        value={query.status}
                        onChange={(e) => patchQuery({ status: e.target.value as UsersTableQuery["status"] })}
                      >
                        <option value="">All Statuses</option>
                        <option value="active">Enabled</option>
                        <option value="disabled">Disabled</option>
                      </select>
                    }
                  />
                  <th>Verified</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {table.rows.map((user) => (
                  <tr key={user.id} className={user.isActive ? undefined : "deleted-row"}>
                    <td>
                      <span className="user-name-cell">
                        <UserAvatar
                          userId={user.id}
                          name={user.displayName}
                          email={user.email}
                          hasPhoto={user.hasPhoto}
                          revision={photoRevision}
                          size="sm"
                        />
                        {user.displayName}
                      </span>
                    </td>
                    <td>{user.fullName || "—"}</td>
                    <td>{user.email}</td>
                    <td>{user.role}</td>
                    <td>{clientColumnLabel(user, clients)}</td>
                    <td>{user.isActive ? "Enabled" : "Disabled"}</td>
                    <td>{user.emailVerified ? "Verified" : "Unverified"}</td>
                    <td className="actions-cell">
                      <button className="ghost" type="button" onClick={() => startEdit(user)}>
                        Edit
                      </button>
                      {!user.emailVerified && (
                        <button className="ghost" type="button" onClick={() => setPendingResend(user)}>
                          Resend verify
                        </button>
                      )}
                      {user.isActive && (
                        <button className="ghost" type="button" onClick={() => setPendingDisable(user)}>
                          Disable
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {table.totalPages > 1 && (
            <div className="users-pager">
              <button className="ghost" type="button" disabled={table.page <= 1} onClick={() => patchQuery({ page: table.page - 1 })}>
                Previous
              </button>
              <span>
                Page {table.page} of {table.totalPages}
              </span>
              <button
                className="ghost"
                type="button"
                disabled={table.page >= table.totalPages}
                onClick={() => patchQuery({ page: table.page + 1 })}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}

      {editing && (
        <form className="panel" onSubmit={onSubmit}>
          <h2>{editing === "new" ? "New User" : "Edit User"}</h2>
          {editingUser && (
            <PhotoEditor
              userId={editingUser.id}
              name={form.displayName || editingUser.email}
              email={editingUser.email}
              hasPhoto={editingUser.hasPhoto}
              revision={photoRevision}
              error={photoError}
              onUpload={async (file) => {
                setPhotoError(null);
                try {
                  const saved = await endpoints.uploadUserPhoto(editingUser.id, file);
                  setUsers((current) => current.map((user) => (user.id === saved.id ? saved : user)));
                  setPhotoRevision((value) => value + 1);
                  setNotice("Photo updated.");
                } catch (err) {
                  setPhotoError(err instanceof Error ? err.message : "Photo upload failed.");
                }
              }}
              onClear={async () => {
                setPhotoError(null);
                try {
                  const saved = await endpoints.clearUserPhoto(editingUser.id);
                  setUsers((current) => current.map((user) => (user.id === saved.id ? saved : user)));
                  setPhotoRevision((value) => value + 1);
                  setNotice("Photo removed.");
                } catch (err) {
                  setPhotoError(err instanceof Error ? err.message : "Could not remove photo.");
                }
              }}
            />
          )}
          <div className="form-grid">
            <label>
              Display Name
              <input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} required />
            </label>
            <label>
              Full Name
              <input
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                required={editing === "new"}
              />
            </label>
            <label>
              Email
              <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
            </label>
            <label>
              <LabelWithHelp helpKey="users.role">Role</LabelWithHelp>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
                {usersRoles.map((role) => (
                  <option key={role}>{role}</option>
                ))}
              </select>
            </label>
            <PasswordPair
              id="user-password"
              passwordLabel={editing === "new" ? "Password" : "New Password (Optional)"}
              password={form.password}
              confirm={form.confirm}
              required={editing === "new"}
              passwordError={passwordError}
              confirmError={confirmError}
              onPassword={(password) => {
                setForm({ ...form, password });
                if (passwordError) setPasswordError(null);
              }}
              onConfirm={(confirm) => {
                setForm({ ...form, confirm });
                if (confirmError) setConfirmError(null);
              }}
            />
          </div>
          <label className="remember">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
            />
            Active
          </label>
          <fieldset className="access-set">
            <legend>Client Access</legend>
            {clients.map((client) => (
              <label key={client.id} className="remember">
                <input
                  type="checkbox"
                  checked={form.clientIds.includes(client.id)}
                  onChange={() => toggleClient(client.id)}
                />
                {client.name}
              </label>
            ))}
          </fieldset>
          <div className="row-actions">
            <button className="primary" type="submit">
              Save
            </button>
            <button className="ghost" type="button" onClick={() => setEditing(null)}>
              Cancel
            </button>
          </div>
        </form>
      )}

      {pendingResend && (
        <ConfirmSheet
          title="Resend verification email?"
          body={`${pendingResend.displayName} will get a new verification link through the active mail mode. Disabled accounts still cannot sign in.`}
          confirmLabel="Resend"
          danger={false}
          helpKey="users.resendVerification"
          onCancel={() => setPendingResend(null)}
          onConfirm={async () => {
            try {
              const result = await endpoints.resendVerification(pendingResend.id);
              setPendingResend(null);
              setNotice(result.message);
              setError(null);
              await load();
            } catch (err) {
              setPendingResend(null);
              setError(err instanceof Error ? err.message : "Resend failed.");
            }
          }}
        />
      )}

      {pendingDisable && (
        <ConfirmSheet
          title="Disable this user?"
          body={`${pendingDisable.displayName} will not be able to sign in. An Admin can re-enable them later.`}
          confirmLabel="Disable"
          onCancel={() => setPendingDisable(null)}
          onConfirm={async () => {
            try {
              await endpoints.disableUser(pendingDisable.id);
              setPendingDisable(null);
              setNotice("User disabled.");
              setError(null);
              await load();
            } catch (err) {
              setPendingDisable(null);
              setError(err instanceof Error ? err.message : "Disable failed.");
            }
          }}
        />
      )}
    </section>
  );
}

function SortFilterTh({
  label,
  sortKey,
  query,
  onSort,
  filter
}: {
  label: string;
  sortKey: UsersSortKey;
  query: UsersTableQuery;
  onSort: (key: UsersSortKey) => void;
  filter?: ReactNode;
}) {
  const active = query.sort === sortKey;
  const ariaSort = active ? (query.dir === "asc" ? "ascending" : "descending") : "none";
  return (
    <th className="users-th" aria-sort={ariaSort}>
      <button type="button" className={`th-sort${active ? " is-active" : ""}`} onClick={() => onSort(sortKey)}>
        {label}
        <span className="th-sort-affordance" aria-hidden="true">
          {active ? (query.dir === "asc" ? "▲" : "▼") : "↕"}
        </span>
      </button>
      {filter}
    </th>
  );
}
