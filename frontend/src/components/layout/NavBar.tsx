import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';

export function NavBar() {
  const { user, logout } = useAuth();
  return (
    <nav className="bg-canvas border-b border-line px-6 py-3 flex items-center justify-between">
      <Link to="/" className="flex items-center gap-2 font-semibold text-ink">
        <span className="w-2 h-2 rounded-full bg-primary-600" aria-hidden="true" />
        Locaccessum
      </Link>
      <div className="flex items-center gap-5 text-sm">
        <Link to="/" className="text-gray-600 hover:text-ink">Mes inventaires</Link>
        <Link to="/profile" className="text-gray-600 hover:text-ink">{user?.displayName}</Link>
        <button onClick={logout} className="text-gray-600 hover:text-ink">Déconnexion</button>
      </div>
    </nav>
  );
}
