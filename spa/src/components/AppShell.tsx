import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";

export default function AppShell() {
  const { me, logout, canUpload, canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">Deed AI</div>
        <nav>
          <NavLink to="/dashboard">Dashboard</NavLink>
          <NavLink to="/documents">Documents</NavLink>
          {canUpload ? (
            <NavLink to="/upload">Upload</NavLink>
          ) : (
            <button
              className="nav-disabled"
              type="button"
              onClick={() => navigate("/denied", { state: { action: "upload documents" } })}
            >
              Upload
            </button>
          )}
          <NavLink to="/reports">Reports</NavLink>
          <NavLink to="/software">Software</NavLink>
          {canEdit ? (
            <NavLink to="/sales">Sales</NavLink>
          ) : (
            <button
              className="nav-disabled"
              type="button"
              onClick={() => navigate("/denied", { state: { action: "open the Sales tab" } })}
            >
              Sales
            </button>
          )}
          {canAdmin ? (
            <NavLink to="/restore">Restore</NavLink>
          ) : (
            <button
              className="nav-disabled"
              type="button"
              onClick={() => navigate("/denied", { state: { action: "restore or hard-delete deeds" } })}
            >
              Restore
            </button>
          )}
          {canAdmin ? (
            <NavLink to="/users">Users</NavLink>
          ) : (
            <button
              className="nav-disabled"
              type="button"
              onClick={() => navigate("/denied", { state: { action: "manage users" } })}
            >
              Users
            </button>
          )}
          {canAdmin ? (
            <NavLink to="/settings">Settings</NavLink>
          ) : (
            <button
              className="nav-disabled"
              type="button"
              onClick={() => navigate("/denied", { state: { action: "change settings" } })}
            >
              Settings
            </button>
          )}
        </nav>
        <button
          className="ghost sidebar-logout"
          type="button"
          onClick={() => {
            logout();
            navigate("/login");
          }}
        >
          Sign out
        </button>
      </aside>
      <div className="main">
        <header className="topbar">
          <span className="role-pill">{me?.role}</span>
        </header>
        <Outlet />
      </div>
    </div>
  );
}
