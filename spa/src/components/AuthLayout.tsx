import { Outlet } from "react-router-dom";
import SiteFooter from "./SiteFooter";

export default function AuthLayout() {
  return (
    <div className="login-page">
      <div className="login-page-body">
        <Outlet />
      </div>
      <SiteFooter />
    </div>
  );
}
