import { Outlet } from "react-router-dom";

export default function AuthLayout() {
  return (
    <div className="login-page">
      <header className="topbar login-topbar">
        <h1 className="topbar-title">Deed AI</h1>
      </header>
      <div className="login-page-body">
        <Outlet />
      </div>
    </div>
  );
}
