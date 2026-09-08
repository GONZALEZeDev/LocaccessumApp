import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

/** Gate for /login and /register: an already-authenticated user is sent to the dashboard instead. */
export function PublicOnlyRoute() {
  const { user, loading } = useAuth();
  if (loading) {
    return <div className="min-h-screen flex items-center justify-center text-primary-700">Chargement...</div>;
  }
  if (user) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
