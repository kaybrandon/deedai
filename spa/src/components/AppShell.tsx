import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";
import UserAvatar from "./UserAvatar";
import SiteFooter from "./SiteFooter";
import {
  IconBell,
  IconDashboard,
  IconDocuments,
  IconLogout,
  IconMenuFold,
  IconMenuUnfold,
  IconReports,
  IconRestore,
  IconSales,
  IconSettings,
  IconUpload,
  IconUser
} from "./GisIcons";

const SIDER_KEY = "deedai.siderCollapsed";

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
  return active ? "active ant-menu-item-selected" : undefined;
}

function navClass({ isActive }: { isActive: boolean }) {
  return `ant-menu-item${isActive ? " ant-menu-item-selected active" : ""}`;
}

export default function AppShell() {
  const { me, logout, canUpload, canAdmin, canEdit } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDER_KEY) === "1");
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [noticeOpen, setNoticeOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement | null>(null);
  const noticeRef = useRef<HTMLDivElement | null>(null);
  const onSettingsSection = isSettingsSection(location.pathname);
  const [settingsOpen, setSettingsOpen] = useState(true);
  const [systemOpen, setSystemOpen] = useState(true);
  const title = productTitle(me?.clients);
  const identity = me?.displayName || me?.email || "signed-in user";
  const scope = me?.clients?.length === 1 ? me.clients[0].name : "All Clients";
  const headerBrand = `BIS Consultants · ${scope}`;
  const onSystem = location.pathname === "/settings" && location.hash !== "#swagger";
  const onApiDocs = location.pathname === "/settings" && location.hash === "#swagger";
  const onSoftware = location.pathname === "/software" || location.pathname.startsWith("/software/");
  const onUsers = location.pathname === "/users" || location.pathname.startsWith("/users/");
  const onSystemSection = onSystem || onApiDocs || onSoftware || onUsers;

  function closeNav() {
    setNavOpen(false);
  }

  function setSiderCollapsed(next: boolean) {
    setCollapsed(next);
    localStorage.setItem(SIDER_KEY, next ? "1" : "0");
  }

  useEffect(() => {
    if (onSettingsSection) {
      setSettingsOpen(true);
    }
    if (onSystemSection) {
      setSystemOpen(true);
    }
  }, [onSettingsSection, onSystemSection]);

  useEffect(() => {
    function onDocClick(event: MouseEvent) {
      const target = event.target as Node;
      if (userMenuRef.current && !userMenuRef.current.contains(target)) {
        setUserMenuOpen(false);
      }
      if (noticeRef.current && !noticeRef.current.contains(target)) {
        setNoticeOpen(false);
      }
    }
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  const menu = (
    <ul className="ant-menu ant-menu-dark ant-menu-inline ant-menu-root" onClick={closeNav} role="menu">
      <li role="none">
        <NavLink to="/dashboard" className={navClass} role="menuitem">
          <span className="ant-menu-item-icon"><IconDashboard /></span>
          <span className="ant-menu-title-content">Dashboard</span>
        </NavLink>
      </li>
      <li role="none">
        <NavLink to="/documents" className={navClass} role="menuitem">
          <span className="ant-menu-item-icon"><IconDocuments /></span>
          <span className="ant-menu-title-content">Documents</span>
        </NavLink>
      </li>
      <li role="none">
        {canUpload ? (
          <NavLink to="/upload" className={navClass} role="menuitem">
            <span className="ant-menu-item-icon"><IconUpload /></span>
            <span className="ant-menu-title-content">Upload</span>
          </NavLink>
        ) : (
          <button
            className="nav-disabled ant-menu-item"
            type="button"
            role="menuitem"
            onClick={() => navigate("/denied", { state: { action: "upload documents" } })}
          >
            <span className="ant-menu-item-icon"><IconUpload /></span>
            <span className="ant-menu-title-content">Upload</span>
          </button>
        )}
      </li>
      <li role="none">
        <NavLink to="/reports" className={navClass} role="menuitem">
          <span className="ant-menu-item-icon"><IconReports /></span>
          <span className="ant-menu-title-content">Reports</span>
        </NavLink>
      </li>
      <li role="none">
        {canEdit ? (
          <NavLink to="/sales" className={navClass} role="menuitem">
            <span className="ant-menu-item-icon"><IconSales /></span>
            <span className="ant-menu-title-content">Sales</span>
          </NavLink>
        ) : (
          <button
            className="nav-disabled ant-menu-item"
            type="button"
            role="menuitem"
            onClick={() => navigate("/denied", { state: { action: "open the Sales tab" } })}
          >
            <span className="ant-menu-item-icon"><IconSales /></span>
            <span className="ant-menu-title-content">Sales</span>
          </button>
        )}
      </li>
      <li role="none">
        {canAdmin ? (
          <NavLink to="/restore" className={navClass} role="menuitem">
            <span className="ant-menu-item-icon"><IconRestore /></span>
            <span className="ant-menu-title-content">Restore</span>
          </NavLink>
        ) : (
          <button
            className="nav-disabled ant-menu-item"
            type="button"
            role="menuitem"
            onClick={() => navigate("/denied", { state: { action: "restore or hard-delete deeds" } })}
          >
            <span className="ant-menu-item-icon"><IconRestore /></span>
            <span className="ant-menu-title-content">Restore</span>
          </button>
        )}
      </li>
      <li className="nav-group ant-menu-submenu" onClick={(e) => e.stopPropagation()}>
        <button
          className="nav-group-toggle ant-menu-submenu-title"
          type="button"
          aria-expanded={settingsOpen}
          aria-controls="settings-nav"
          onClick={() => setSettingsOpen((open) => !open)}
        >
          <span className="ant-menu-item-icon"><IconSettings /></span>
          <span className="ant-menu-title-content">Settings</span>
          <span className="nav-group-caret" aria-hidden="true">{settingsOpen ? "▾" : "▸"}</span>
        </button>
        {settingsOpen && (
          <ul className="nav-sub ant-menu ant-menu-sub ant-menu-inline" id="settings-nav">
            <li className="nav-group" data-nav="system-mid">
              <div className="nav-mid-row">
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
              </div>
              {systemOpen && (
                <ul className="nav-sub ant-menu ant-menu-sub ant-menu-inline" id="system-nav">
                  <li>
                    <NavLink to="/software" className={() => childClass(onSoftware)} onClick={closeNav}>
                      Software
                    </NavLink>
                  </li>
                  <li>
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
                  </li>
                  {canAdmin && (
                    <li>
                      <NavLink to="/settings#swagger" className={() => childClass(onApiDocs)} onClick={closeNav}>
                        API
                      </NavLink>
                    </li>
                  )}
                </ul>
              )}
            </li>
          </ul>
        )}
      </li>
    </ul>
  );

  return (
    <div
      className={`app-shell ant-layout ant-layout-has-sider shell${navOpen ? " is-nav-open" : ""}${collapsed ? " is-sider-collapsed" : ""}`}
      data-chrome="gis"
      style={{ minHeight: "100vh" }}
    >
      <aside
        className="ant-layout-sider ant-layout-sider-dark sidebar"
        id="app-sidebar"
        style={{ background: "#001529", width: collapsed ? 64 : 220, flex: `0 0 ${collapsed ? 64 : 220}px` }}
      >
        <div className="ant-layout-sider-children">
          <div className="brand">
            <span className="brand-mark" aria-hidden="true">
              D
            </span>
            {!collapsed && <span>Deed AI</span>}
          </div>
          <nav className="sider-nav" aria-label="Deed AI">
            {menu}
          </nav>
          <div className="sidebar-identity visually-hidden">
            <p className="sidebar-logged-in">Logged In As {identity}</p>
            <NavLink className="sidebar-profile" to="/profile" onClick={closeNav}>
              My Profile
            </NavLink>
          </div>
        </div>
      </aside>
      {navOpen && <button className="nav-backdrop" type="button" aria-label="Close menu" onClick={closeNav} />}
      <div className="ant-layout shell-body main">
        <header className="topbar app-header ant-layout-header">
          <div className="app-header-left">
            <button
              className="app-header-icon-btn ant-btn ant-btn-text nav-toggle"
              type="button"
              aria-expanded={navOpen || !collapsed}
              aria-controls="app-sidebar"
              aria-label={collapsed ? "Expand menu" : "Collapse menu"}
              onClick={() => {
                if (window.matchMedia("(max-width: 768px)").matches) {
                  setNavOpen((open) => !open);
                  return;
                }
                setSiderCollapsed(!collapsed);
              }}
            >
              {collapsed ? <IconMenuUnfold /> : <IconMenuFold />}
            </button>
            <span className="app-header-brand topbar-title app-header-brand-desktop">{headerBrand}</span>
            <span className="app-header-brand topbar-title app-header-brand-phone">{title}</span>
          </div>
          <div className="app-header-right topbar-meta">
            <div className="header-popover" ref={noticeRef}>
              <button
                className="app-header-icon-btn ant-btn ant-btn-text"
                type="button"
                aria-label="Notifications"
                aria-expanded={noticeOpen}
                onClick={() => {
                  setNoticeOpen((open) => !open);
                  setUserMenuOpen(false);
                }}
              >
                <IconBell />
              </button>
              {noticeOpen && (
                <div className="notification-panel" role="dialog" aria-label="Notifications">
                  <div className="notification-panel-head">
                    <strong>Notifications</strong>
                  </div>
                  <p className="notification-empty">No notifications.</p>
                </div>
              )}
            </div>
            <div className="header-popover" ref={userMenuRef}>
              <button
                className="app-header-user ant-btn ant-btn-text"
                type="button"
                aria-expanded={userMenuOpen}
                aria-haspopup="menu"
                onClick={() => {
                  setUserMenuOpen((open) => !open);
                  setNoticeOpen(false);
                }}
              >
                <UserAvatar
                  userId={me?.id}
                  name={me?.displayName}
                  email={me?.email}
                  hasPhoto={me?.hasPhoto}
                  size="sm"
                />
                <span className="topbar-name">{identity}</span>
              </button>
              {userMenuOpen && (
                <ul className="ant-dropdown-menu header-user-menu" role="menu">
                  <li className="header-user-menu-meta" role="none">
                    Logged In As {identity}
                  </li>
                  <li role="none">
                    <NavLink
                      className="sidebar-profile header-menu-item"
                      to="/profile"
                      role="menuitem"
                      onClick={() => setUserMenuOpen(false)}
                    >
                      <IconUser />
                      My Profile
                    </NavLink>
                  </li>
                  <li className="ant-dropdown-menu-item-divider" role="separator" />
                  <li role="none">
                    <button
                      className="header-menu-item"
                      type="button"
                      role="menuitem"
                      onClick={() => {
                        setUserMenuOpen(false);
                        logout();
                        navigate("/login");
                      }}
                    >
                      <IconLogout />
                      Sign Out
                    </button>
                  </li>
                </ul>
              )}
            </div>
          </div>
        </header>
        <div className="content-wrap ant-layout-content main-body">
          <Outlet />
        </div>
        <footer className="app-footer ant-layout-footer">
          <SiteFooter />
        </footer>
      </div>
    </div>
  );
}
