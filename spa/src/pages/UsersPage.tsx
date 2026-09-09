import { FormEvent, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ApiError, type ClientItem, type Role, type UserDetail } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import { LabelWithHelp } from "../components/FieldHelp";
import PasswordPair, { passwordPairErrors } from "../components/PasswordPair";
import PhotoEditor from "../components/PhotoEditor";
import UserAvatar from "../components/UserAvatar";

const roles: Role[] = ["Admin", "Editor", "Uploader", "Viewer"];

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
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  async function load() {
    setUsers(await endpoints.adminUsers());
    setClients(await endpoints.settingsClients());
  }

  useEffect(() => {
    if (!canAdmin) {
      navigate("/denied", { state: { action: "manage users" } });
      return;
    }
    load().catch((err) => setError(err instanceof Error ? err.message : "Could not load users."));
  }, [canAdmin, navigate]);

  const groups = useMemo(() => {
    const byClient = clients
      .map((client) => ({
        client,
        users: users.filter((user) => user.clientIds.includes(client.id))
      }))
      .filter((group) => group.users.length > 0);
    const unassigned = users.filter((user) => user.clientIds.length === 0);
    return { byClient, unassigned };
  }, [clients, users]);

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

  function toggleGroup(id: string) {
    setCollapsed((current) => ({ ...current, [id]: !current[id] }));
  }

  const editingUser = editing && editing !== "new" ? users.find((user) => user.id === editing) : null;

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <h1>Users</h1>
          <p className="page-kicker">Roles and Client access, grouped by Client. Multi-Client users appear under each assigned Client.</p>
        </div>
        <button className="primary" type="button" onClick={startCreate}>
          Add User
        </button>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      {users.length === 0 ? (
        <EmptyState title="No Users Yet" body="Admins can create accounts and map Client access." />
      ) : (
        <div className="user-groups">
          {groups.byClient.map((group) => (
            <UserGroup
              key={group.client.id}
              title={group.client.name}
              users={group.users}
              clients={clients}
              collapsed={Boolean(collapsed[group.client.id])}
              onToggle={() => toggleGroup(group.client.id)}
              onEdit={startEdit}
              onDisable={setPendingDisable}
              onResend={setPendingResend}
              revision={photoRevision}
            />
          ))}
          {groups.unassigned.length > 0 && (
            <UserGroup
              title="No Client access"
              users={groups.unassigned}
              clients={clients}
              collapsed={Boolean(collapsed.unassigned)}
              onToggle={() => toggleGroup("unassigned")}
              onEdit={startEdit}
              onDisable={setPendingDisable}
              onResend={setPendingResend}
              revision={photoRevision}
            />
          )}
        </div>
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
                {roles.map((role) => (
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

function UserGroup({
  title,
  users,
  clients,
  collapsed,
  onToggle,
  onEdit,
  onDisable,
  onResend,
  revision
}: {
  title: string;
  users: UserDetail[];
  clients: ClientItem[];
  collapsed: boolean;
  onToggle: () => void;
  onEdit: (user: UserDetail) => void;
  onDisable: (user: UserDetail) => void;
  onResend: (user: UserDetail) => void;
  revision: number;
}) {
  return (
    <section className="user-group">
      <button
        className="user-group-toggle"
        type="button"
        aria-expanded={!collapsed}
        onClick={onToggle}
      >
        <span className="nav-group-caret" aria-hidden="true">{collapsed ? "▸" : "▾"}</span>
        {title}
        <span className="user-group-count">{users.length}</span>
      </button>
      {!collapsed && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Full Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Clients</th>
                <th>Status</th>
                <th>Verified</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.id} className={user.isActive ? undefined : "deleted-row"}>
                  <td>
                    <span className="user-name-cell">
                      <UserAvatar
                        userId={user.id}
                        name={user.displayName}
                        email={user.email}
                        hasPhoto={user.hasPhoto}
                        revision={revision}
                        size="sm"
                      />
                      {user.displayName}
                    </span>
                  </td>
                  <td>{user.fullName || "—"}</td>
                  <td>{user.email}</td>
                  <td>{user.role}</td>
                  <td>
                    {user.clientIds
                      .map((id) => clients.find((c) => c.id === id)?.name ?? id)
                      .join(", ") || "—"}
                  </td>
                  <td>{user.isActive ? "Active" : "Disabled"}</td>
                  <td>{user.emailVerified ? "Verified" : "Unverified"}</td>
                  <td className="actions-cell">
                    <button className="ghost" type="button" onClick={() => onEdit(user)}>
                      Edit
                    </button>
                    {!user.emailVerified && (
                      <button className="ghost" type="button" onClick={() => onResend(user)}>
                        Resend verify
                      </button>
                    )}
                    {user.isActive && (
                      <button className="ghost" type="button" onClick={() => onDisable(user)}>
                        Disable
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
