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
      <ul className="bg-white border border-line rounded-md divide-y divide-line">
        {invitations.map((inv) => (
          <li key={inv.id} className="flex items-center justify-between text-sm px-4 py-3">
            <span className="text-ink">
              {inv.role} <span className={inv.status === 'Pending' ? 'text-amber-700' : 'text-gray-500'}>— {inv.status}</span>
            </span>
            {inv.status === 'Pending' && (
              <Button variant="secondary" onClick={() => handleRevoke(inv.id)}>Révoquer</Button>
            )}
          </li>
        ))}
        {invitations.length === 0 && <li className="px-4 py-3 text-sm text-gray-600">Aucune invitation.</li>}
      </ul>

      <form onSubmit={handleInvite} className="bg-white border border-line rounded-md p-4 space-y-3 max-w-md">
        <h3 className="text-sm font-semibold text-ink">Inviter quelqu'un</h3>
        <TextField label="Code utilisateur" required value={userCode} onChange={setUserCode} />
        <label className="block text-sm font-medium text-ink">
          Rôle
          <select
            value={role}
            onChange={(e) => setRole(e.target.value as InvitableRole)}
            className="mt-1 w-full rounded-md border border-line bg-white px-3 py-2 text-sm text-ink"
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
