import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { endpoints, type ClientItem, type Role, type UserDetail } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";

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

  async function load() {
    setUsers(await endpoints.adminUsers());
    setClients(await endpoints.clients());
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
    setForm({ ...blank, clientIds: clients.map((c) => c.id) });
  }

  function startEdit(user: UserDetail) {
    setEditing(user.id);
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
      setError(err instanceof Error ? err.message : "Save failed.");
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
                      <button
                        className="ghost"
                        type="button"
                        onClick={async () => {
                          await endpoints.disableUser(user.id);
                          setNotice("User disabled.");
                          await load();
                        }}
                      >
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
            <label>
              {editing === "new" ? "Password" : "New password (optional)"}
              <input
                type="password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                required={editing === "new"}
                minLength={editing === "new" ? 8 : undefined}
              />
            </label>
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
    </section>
  );
}
