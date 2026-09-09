import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";
import UserAvatar from "./UserAvatar";

function isSettingsSection(pathname: string) {
  return pathname === "/settings" || pathname === "/software" || pathname === "/users"
    || pathname.startsWith("/settings/") || pathname.startsWith("/software/") || pathname.startsWith("/users/");
}

function productTitle(clients: { name: string }[] | undefined) {
  if (clients?.length === 1) {
    return `${clients[0].name} Deed AI`;
  }
  return "Deed AI";
}

function childClass(active: boolean) {
  return active ? "active" : undefined;
}

export default function AppShell() {
  const { me, logout, canUpload, canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const onSettingsSection = isSettingsSection(location.pathname);
  const [settingsOpen, setSettingsOpen] = useState(true);
  const [systemOpen, setSystemOpen] = useState(true);
  const title = productTitle(me?.clients);
  const identity = me?.displayName || me?.email || "signed-in user";
  const onSystem = location.pathname === "/settings" && location.hash !== "#swagger";
  const onApiDocs = location.pathname === "/settings" && location.hash === "#swagger";
  const onSoftware = location.pathname === "/software" || location.pathname.startsWith("/software/");
  const onUsers = location.pathname === "/users" || location.pathname.startsWith("/users/");
  const onSystemSection = onSystem || onApiDocs || onSoftware || onUsers;

  function closeNav() {
    setNavOpen(false);
  }

  useEffect(() => {
    if (onSettingsSection) {
      setSettingsOpen(true);
    }
    if (onSystemSection) {
      setSystemOpen(true);
    }
  }, [onSettingsSection, onSystemSection]);

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
          <div className="nav-group" onClick={(e) => e.stopPropagation()}>
            <button
              className="nav-group-toggle"
              type="button"
              aria-expanded={settingsOpen}
              aria-controls="settings-nav"
              onClick={() => setSettingsOpen((open) => !open)}
            >
              Settings
              <span className="nav-group-caret" aria-hidden="true">{settingsOpen ? "▾" : "▸"}</span>
            </button>
            {settingsOpen && (
              <div className="nav-sub" id="settings-nav">
                <div className="nav-group" data-nav="system-mid">
                  <div className="nav-mid-row">
                    <button
                      className="nav-group-caret-only"
                      type="button"
                      aria-expanded={systemOpen}
                      aria-controls="system-nav"
                      aria-label={systemOpen ? "Collapse System" : "Expand System"}
                      onClick={() => setSystemOpen((open) => !open)}
                    >
                      <span className="nav-group-caret" aria-hidden="true">{systemOpen ? "▾" : "▸"}</span>
                    </button>
                    {canAdmin ? (
                      <NavLink to="/settings" end className={() => childClass(onSystem)} onClick={closeNav}>
                        System
                      </NavLink>
                    ) : (
                      <button
                        className="nav-mid-label"
                        type="button"
                        onClick={() => navigate("/denied", { state: { action: "change settings" } })}
                      >
                        System
                      </button>
                    )}
                  </div>
                  {systemOpen && (
                    <div className="nav-sub" id="system-nav">
                      <NavLink to="/software" className={() => childClass(onSoftware)} onClick={closeNav}>
                        Software
                      </NavLink>
                      {canAdmin ? (
                        <NavLink to="/users" className={() => childClass(onUsers)} onClick={closeNav}>
                          Users
                        </NavLink>
                      ) : (
                        <button
                          className="nav-disabled"
                          type="button"
                          onClick={() => navigate("/denied", { state: { action: "manage users" } })}
                        >
                          Users
                        </button>
                      )}
                      {canAdmin && (
                        <NavLink to="/settings#swagger" className={() => childClass(onApiDocs)} onClick={closeNav}>
                          API
                        </NavLink>
                      )}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </nav>
        <div className="sidebar-identity">
          <p className="sidebar-logged-in">Logged In As {identity}</p>
          <NavLink className="sidebar-profile" to="/profile" onClick={closeNav}>
            My Profile
          </NavLink>
        </div>
        <button
          className="ghost sidebar-logout"
          type="button"
          onClick={() => {
            logout();
            navigate("/login");
          }}
        >
          Sign Out
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
          <h1 className="topbar-title">{title}</h1>
          <div className="topbar-meta">
            <UserAvatar
              userId={me?.id}
              name={me?.displayName}
              email={me?.email}
              hasPhoto={me?.hasPhoto}
              size="sm"
            />
            <span className="topbar-name">{me?.displayName ?? me?.email}</span>
            <span className="role-pill">{me?.role}</span>
          </div>
        </header>
        <div className="main-body">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
