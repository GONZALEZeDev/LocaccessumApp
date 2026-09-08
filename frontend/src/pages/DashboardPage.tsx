import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  listInventories,
  createInventory,
  type InventoryListItemResponse,
} from '../api/inventories';
import {
  listMyInvitations,
  acceptInvitation,
  declineInvitation,
  type InvitationResponse,
} from '../api/invitations';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function DashboardPage() {
  const [inventories, setInventories] = useState<InventoryListItemResponse[]>([]);
  const [invitations, setInvitations] = useState<InvitationResponse[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  async function refresh() {
    try {
      const [inv, invites] = await Promise.all([listInventories(), listMyInvitations()]);
      setInventories(inv);
      setInvitations(invites);
      setLoadError(null);
    } catch {
      setLoadError('Impossible de charger vos données, réessayez.');
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setCreating(true);
    try {
      await createInventory(name, description || null);
      setName('');
      setDescription('');
      await refresh();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setCreating(false);
    }
  }

  async function handleAccept(id: string) {
    try {
      await acceptInvitation(id);
      await refresh();
    } catch (err) {
      setLoadError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleDecline(id: string) {
    try {
      await declineInvitation(id);
      await refresh();
    } catch (err) {
      setLoadError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  return (
    <div className="space-y-8">
      {loadError && <ErrorText message={loadError} />}

      {invitations.length > 0 && (
        <section className="bg-white rounded-lg shadow p-4">
          <h2 className="font-semibold text-primary-800 mb-2">Invitations reçues</h2>
          <ul className="space-y-2">
            {invitations.map((inv) => (
              <li key={inv.id} className="flex items-center justify-between text-sm">
                <span>
                  {inv.inventoryName} — rôle {inv.role}
                </span>
                <span className="flex gap-2">
                  <Button variant="primary" onClick={() => handleAccept(inv.id)}>Accepter</Button>
                  <Button variant="secondary" onClick={() => handleDecline(inv.id)}>Refuser</Button>
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section>
        <h1 className="text-xl font-semibold text-primary-800 mb-4">Mes inventaires</h1>
        <ul className="space-y-2">
          {inventories.map((inv) => (
            <li key={inv.id}>
              <Link
                to={`/inventories/${inv.id}`}
                className="block bg-white rounded-lg shadow p-4 hover:bg-primary-50"
              >
                <span className="font-medium text-primary-800">{inv.name}</span>
                <span className="text-sm text-gray-600 ml-2">
                  ({inv.myRole}, {inv.memberCount} membre{inv.memberCount > 1 ? 's' : ''})
                </span>
              </Link>
            </li>
          ))}
          {inventories.length === 0 && <p className="text-sm text-gray-600">Aucun inventaire pour le moment.</p>}
        </ul>
      </section>

      <section className="bg-white rounded-lg shadow p-4">
        <h2 className="font-semibold text-primary-800 mb-2">Créer un inventaire</h2>
        <form onSubmit={handleCreate} className="space-y-3">
          <ErrorText message={formError} />
          <TextField label="Nom" required value={name} onChange={setName} />
          <TextField label="Description (optionnelle)" value={description} onChange={setDescription} />
          <Button type="submit" disabled={creating}>{creating ? 'Création...' : 'Créer'}</Button>
        </form>
      </section>
    </div>
  );
}
