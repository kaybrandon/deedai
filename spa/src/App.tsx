import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./auth";
import AppShell from "./components/AppShell";
import DashboardPage from "./pages/DashboardPage";
import DeniedPage from "./pages/DeniedPage";
import DocumentsPage from "./pages/DocumentsPage";
import LoginPage from "./pages/LoginPage";
import ReviewPage from "./pages/ReviewPage";
import UploadPage from "./pages/UploadPage";

export default function App() {
  const { ready, me } = useAuth();
  if (!ready) {
    return <div className="boot">Loading Deed AI…</div>;
  }

  return (
    <Routes>
      <Route path="/login" element={me ? <Navigate to="/dashboard" replace /> : <LoginPage />} />
      <Route path="/denied" element={<DeniedPage />} />
      <Route element={me ? <AppShell /> : <Navigate to="/login" replace />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/documents" element={<DocumentsPage />} />
        <Route path="/documents/:id" element={<ReviewPage />} />
        <Route path="/upload" element={<UploadPage />} />
      </Route>
      <Route path="*" element={<Navigate to={me ? "/dashboard" : "/login"} replace />} />
    </Routes>
  );
}
