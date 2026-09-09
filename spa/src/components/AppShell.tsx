import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";

export default function AppShell() {
  const { me, logout, canUpload, canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();
  const [navOpen, setNavOpen] = useState(false);

  function closeNav() {
    setNavOpen(false);
  }

  return (
    <div className={`shell${navOpen ? " is-nav-open" : ""}`}>
      <aside className="sidebar" id="app-sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            D
          </span>
          Deed AI
        </div>
        <nav onClick={closeNav}>
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
          {canAdmin && <NavLink to="/settings#swagger">API</NavLink>}
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
      {navOpen && <button className="nav-backdrop" type="button" aria-label="Close menu" onClick={closeNav} />}
      <div className="main">
        <header className="topbar">
          <button
            className="ghost nav-toggle"
            type="button"
            aria-expanded={navOpen}
            aria-controls="app-sidebar"
            onClick={() => setNavOpen((open) => !open)}
          >
            Menu
          </button>
          <div className="topbar-meta">
            <span className="topbar-name">{me?.displayName ?? me?.email}</span>
            <span className="role-pill">{me?.role}</span>
          </div>
        </header>
        <Outlet />
      </div>
    </div>
  );
}
