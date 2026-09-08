import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';

export function NavBar() {
  const { user, logout } = useAuth();
  return (
    <nav className="bg-primary-100 border-b border-primary-200 px-6 py-3 flex items-center justify-between">
      <Link to="/" className="font-semibold text-primary-800">Locaccessum</Link>
      <div className="flex items-center gap-4 text-sm">
        <Link to="/" className="text-primary-700 hover:text-primary-900">Mes inventaires</Link>
        <Link to="/profile" className="text-primary-700 hover:text-primary-900">{user?.displayName}</Link>
        <button onClick={logout} className="text-primary-700 hover:text-primary-900 underline">Déconnexion</button>
      </div>
    </nav>
  );
}
