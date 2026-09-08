import { useEffect, useState, type FormEvent } from 'react';
import { listMembers, updateMemberRole, removeMember, type MemberResponse } from '../../api/members';
import { transferOwnership } from '../../api/inventories';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

export function MembersTab({
  inventoryId,
  canManage,
  isOwner,
}: {
  inventoryId: string;
  canManage: boolean;
  isOwner: boolean;
}) {
  const [members, setMembers] = useState<MemberResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [transferTo, setTransferTo] = useState('');
  const [transferring, setTransferring] = useState(false);

  async function refresh() {
    try {
      setMembers(await listMembers(inventoryId));
      setError(null);
    } catch {
      setError('Impossible de charger les membres.');
    }
  }

  useEffect(() => {
    refresh();
  }, [inventoryId]);

  async function handleRoleChange(userId: string, role: 'Admin' | 'Member') {
    try {
      await updateMemberRole(inventoryId, userId, role);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleRemove(userId: string) {
    try {
      await removeMember(inventoryId, userId);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleTransfer(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setTransferring(true);
    try {
      await transferOwnership(inventoryId, transferTo);
      setTransferTo('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setTransferring(false);
    }
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />
      <ul className="space-y-2">
        {members.map((m) => (
          <li key={m.userId} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              <span className="font-medium text-primary-800">{m.displayName}</span>{' '}
              <span className="text-gray-500">({m.userCode})</span> — {m.role}
            </span>
            {canManage && m.role !== 'Owner' && (
              <span className="flex gap-2 items-center">
                <select
                  value={m.role}
                  onChange={(e) => handleRoleChange(m.userId, e.target.value as 'Admin' | 'Member')}
                  className="border border-gray-300 rounded px-2 py-1 text-xs"
                >
                  <option value="Admin">Admin</option>
                  <option value="Member">Membre</option>
                </select>
                <Button variant="danger" onClick={() => handleRemove(m.userId)}>Retirer</Button>
              </span>
            )}
          </li>
        ))}
      </ul>

      {isOwner && (
        <form onSubmit={handleTransfer} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
          <h3 className="font-semibold text-primary-800">Transférer la propriété</h3>
          <TextField label="ID utilisateur du nouveau propriétaire" required value={transferTo} onChange={setTransferTo} />
          <Button type="submit" variant="danger" disabled={transferring}>
            {transferring ? 'Transfert...' : 'Transférer'}
          </Button>
        </form>
      )}
    </div>
  );
}
