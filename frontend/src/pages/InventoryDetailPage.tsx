import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getInventory, type InventoryResponse } from '../api/inventories';
import { EquipmentTab } from './inventory/EquipmentTab';
import { ReservationsTab } from './inventory/ReservationsTab';
import { MembersTab } from './inventory/MembersTab';
import { InvitationsTab } from './inventory/InvitationsTab';
import { ErrorText } from '../components/forms/ErrorText';

type Tab = 'equipment' | 'reservations' | 'members' | 'invitations';

const TABS: { id: Tab; label: string }[] = [
  { id: 'equipment', label: 'Équipement' },
  { id: 'reservations', label: 'Réservations' },
  { id: 'members', label: 'Membres' },
  { id: 'invitations', label: 'Invitations' },
];

export default function InventoryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [inventory, setInventory] = useState<InventoryResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('equipment');

  useEffect(() => {
    if (!id) return;
    getInventory(id)
      .then(setInventory)
      .catch(() => setError("Impossible de charger cet inventaire."));
  }, [id]);

  if (!id) return null;
  if (error) return <ErrorText message={error} />;
  if (!inventory) return <p className="text-primary-700">Chargement...</p>;

  const canManage = inventory.myRole === 'Owner' || inventory.myRole === 'Admin';

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-primary-800">{inventory.name}</h1>
        {inventory.description && <p className="text-sm text-gray-600">{inventory.description}</p>}
      </div>

      <div className="flex gap-1 border-b border-primary-200">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`px-4 py-2 text-sm font-medium ${
              tab === t.id
                ? 'border-b-2 border-primary-600 text-primary-800'
                : 'text-gray-500 hover:text-primary-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'equipment' && <EquipmentTab inventoryId={id} canManage={canManage} />}
      {tab === 'reservations' && <ReservationsTab inventoryId={id} />}
      {tab === 'members' && <MembersTab inventoryId={id} canManage={canManage} isOwner={inventory.myRole === 'Owner'} />}
      {tab === 'invitations' && <InvitationsTab inventoryId={id} canManage={canManage} />}
    </div>
  );
}
