import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ApiError, type ClientItem, type Role, type UserDetail } from "../api";
import { useAuth } from "../auth";
import ConfirmSheet from "../components/ConfirmSheet";
import EmptyState from "../components/EmptyState";
import PasswordField from "../components/PasswordField";
import { validatePassword } from "../password";

const roles: Role[] = ["Admin", "Editor", "Uploader", "Viewer"];

const blank = {
  email: "",
  displayName: "",
  role: "Viewer" as Role,
  password: "",
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
  const [pendingDisable, setPendingDisable] = useState<UserDetail | null>(null);

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

  function startCreate() {
    setEditing("new");
    setPasswordError(null);
    setForm({ ...blank, clientIds: clients.map((c) => c.id) });
  }

  function startEdit(user: UserDetail) {
    setEditing(user.id);
    setPasswordError(null);
    setForm({
      email: user.email,
      displayName: user.displayName,
      role: user.role,
      password: "",
      isActive: user.isActive,
      clientIds: user.clientIds
    });
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const nextPasswordError = validatePassword(form.password, editing === "new");
    if (nextPasswordError) {
      setPasswordError(nextPasswordError);
      return;
    }
    setPasswordError(null);
    const body = {
      email: form.email,
      displayName: form.displayName,
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

  return (
    <section className="page">
      <div className="review-header">
        <h1>Users</h1>
        <button className="primary" type="button" onClick={startCreate}>
          Add user
        </button>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      {users.length === 0 ? (
        <EmptyState title="No users yet" body="Admins can create accounts and map Client access." />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Clients</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.id} className={user.isActive ? undefined : "deleted-row"}>
                  <td>{user.displayName}</td>
                  <td>{user.email}</td>
                  <td>{user.role}</td>
                  <td>
                    {user.clientIds
                      .map((id) => clients.find((c) => c.id === id)?.name ?? id)
                      .join(", ") || "—"}
                  </td>
                  <td>{user.isActive ? "Active" : "Disabled"}</td>
                  <td className="actions-cell">
                    <button className="ghost" type="button" onClick={() => startEdit(user)}>
                      Edit
                    </button>
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
      )}

      {editing && (
        <form className="panel" onSubmit={onSubmit}>
          <h2>{editing === "new" ? "New user" : "Edit user"}</h2>
          <div className="form-grid">
            <label>
              Display name
              <input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} required />
            </label>
            <label>
              Email
              <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
            </label>
            <label>
              Role
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
                {roles.map((role) => (
                  <option key={role}>{role}</option>
                ))}
              </select>
            </label>
            <PasswordField
              id="user-password"
              label={editing === "new" ? "Password" : "New password (optional)"}
              value={form.password}
              required={editing === "new"}
              error={passwordError}
              onChange={(password) => {
                setForm({ ...form, password });
                if (passwordError) {
                  setPasswordError(null);
                }
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
            <legend>Client access</legend>
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
