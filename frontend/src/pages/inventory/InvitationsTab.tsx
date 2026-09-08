import { useEffect, useState, type FormEvent } from 'react';
import {
  listInventoryInvitations,
  createInvitation,
  revokeInvitation,
  type InvitationResponse,
  type InvitableRole,
} from '../../api/invitations';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

export function InvitationsTab({ inventoryId, canManage }: { inventoryId: string; canManage: boolean }) {
  const [invitations, setInvitations] = useState<InvitationResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [userCode, setUserCode] = useState('');
  const [role, setRole] = useState<InvitableRole>('Member');
  const [inviting, setInviting] = useState(false);

  async function refresh() {
    try {
      setInvitations(await listInventoryInvitations(inventoryId));
      setError(null);
    } catch {
      setError('Impossible de charger les invitations.');
    }
  }

  useEffect(() => {
    if (canManage) refresh();
  }, [inventoryId, canManage]);

  async function handleInvite(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setInviting(true);
    try {
      await createInvitation(inventoryId, userCode, role);
      setUserCode('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setInviting(false);
    }
  }

  async function handleRevoke(id: string) {
    try {
      await revokeInvitation(id);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  if (!canManage) {
    return <p className="text-sm text-gray-600">Seuls les administrateurs peuvent gérer les invitations.</p>;
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />
      <ul className="space-y-2">
        {invitations.map((inv) => (
          <li key={inv.id} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              {inv.role} — {inv.status}
            </span>
            {inv.status === 'Pending' && (
              <Button variant="secondary" onClick={() => handleRevoke(inv.id)}>Révoquer</Button>
            )}
          </li>
        ))}
        {invitations.length === 0 && <p className="text-sm text-gray-600">Aucune invitation.</p>}
      </ul>

      <form onSubmit={handleInvite} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
        <h3 className="font-semibold text-primary-800">Inviter quelqu'un</h3>
        <TextField label="Code utilisateur" required value={userCode} onChange={setUserCode} />
        <label className="block text-sm text-gray-700">
          Rôle
          <select
            value={role}
            onChange={(e) => setRole(e.target.value as InvitableRole)}
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2"
          >
            <option value="Member">Membre</option>
            <option value="Admin">Admin</option>
          </select>
        </label>
        <Button type="submit" disabled={inviting}>{inviting ? 'Envoi...' : 'Inviter'}</Button>
      </form>
    </div>
  );
}
