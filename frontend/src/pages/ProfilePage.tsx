import { useAuth } from '../auth/AuthContext';

export default function ProfilePage() {
  const { user } = useAuth();
  if (!user) return null;

  return (
    <div className="bg-white rounded-lg shadow p-6 max-w-md">
      <h1 className="text-xl font-semibold text-primary-800 mb-4">Mon profil</h1>
      <dl className="space-y-2 text-sm">
        <div>
          <dt className="text-gray-500">Nom affiché</dt>
          <dd className="text-gray-900">{user.displayName}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Email</dt>
          <dd className="text-gray-900">{user.email}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Code utilisateur</dt>
          <dd className="text-gray-900">{user.userCode}</dd>
        </div>
      </dl>
      <p className="text-xs text-gray-500 mt-4">
        Le code utilisateur sert aux autres membres pour vous inviter dans un inventaire.
      </p>
    </div>
  );
}
