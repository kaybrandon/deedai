import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";

export default function DeniedPage() {
  const { me } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const action = (location.state as { action?: string } | null)?.action ?? "perform this action";

  return (
    <div className="login-page">
      <div className="login-card">
        <h1>Access denied</h1>
        <p className="subtitle">Deed AI could not continue with your current role.</p>
        <div className="denied-box" role="alert">
          Access denied. Your {me?.role ?? "unknown"} role cannot {action}.
        </div>
        <button className="primary" type="button" onClick={() => navigate("/dashboard")}>
          Back to dashboard
        </button>
      </div>
    </div>
  );
}
