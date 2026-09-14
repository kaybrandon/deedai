import { Outlet } from "react-router-dom";
import SiteFooter from "./SiteFooter";

export default function AuthLayout() {
  return (
    <div className="login-page login-wrap">
      <div className="login-page-body">
        <Outlet />
      </div>
      <SiteFooter onDark />
    </div>
  );
}
