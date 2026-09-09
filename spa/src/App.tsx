import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./auth";
import AppShell from "./components/AppShell";
import AuthLayout from "./components/AuthLayout";
import SiteFooter from "./components/SiteFooter";
import DashboardPage from "./pages/DashboardPage";
import DeniedPage from "./pages/DeniedPage";
import DocumentsPage from "./pages/DocumentsPage";
import ForgotPasswordPage from "./pages/ForgotPasswordPage";
import LoginPage from "./pages/LoginPage";
import ReportsPage from "./pages/ReportsPage";
import ResetPasswordPage from "./pages/ResetPasswordPage";
import RestorePage from "./pages/RestorePage";
import ReviewPage from "./pages/ReviewPage";
import SalesPage from "./pages/SalesPage";
import SettingsPage from "./pages/SettingsPage";
import SoftwarePage from "./pages/SoftwarePage";
import UploadPage from "./pages/UploadPage";
import UsersPage from "./pages/UsersPage";

export default function App() {
  const { ready, me } = useAuth();
  if (!ready) {
    return (
      <div className="boot-page">
        <div className="boot">Loading Deed AI…</div>
        <SiteFooter />
      </div>
    );
  }

  return (
    <Routes>
      <Route element={<AuthLayout />}>
        <Route path="/login" element={me ? <Navigate to="/dashboard" replace /> : <LoginPage />} />
        <Route path="/forgot-password" element={me ? <Navigate to="/dashboard" replace /> : <ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/denied" element={<DeniedPage />} />
      </Route>
      <Route element={me ? <AppShell /> : <Navigate to="/login" replace />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/documents" element={<DocumentsPage />} />
        <Route path="/documents/:id" element={<ReviewPage />} />
        <Route path="/upload" element={<UploadPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/software" element={<SoftwarePage />} />
        <Route path="/sales" element={<SalesPage />} />
        <Route path="/restore" element={<RestorePage />} />
        <Route path="/users" element={<UsersPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to={me ? "/dashboard" : "/login"} replace />} />
    </Routes>
  );
}
