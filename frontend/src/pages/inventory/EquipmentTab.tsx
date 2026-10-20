import { useEffect, useState, type FormEvent } from 'react';
import {
  listEquipmentGrouped,
  getEquipment,
  createEquipment,
  updateEquipment,
  deleteEquipment,
  type EquipmentStackResponse,
  type EquipmentResponse,
  type EquipmentStatus,
} from '../../api/equipment';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

const STATUS_LABEL: Record<EquipmentStatus, string> = {
  Active: 'Disponible',
  Maintenance: 'En maintenance',
  Retired: 'Retiré',
};

const STATUS_DOT: Record<EquipmentStatus, string> = {
  Active: 'bg-primary-600',
  Maintenance: 'bg-amber-600',
  Retired: 'bg-gray-400',
};

const STATUS_TEXT: Record<EquipmentStatus, string> = {
  Active: 'text-primary-700',
  Maintenance: 'text-amber-700',
  Retired: 'text-gray-500',
};

function StatusBadge({ status }: { status: EquipmentStatus }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-sm">
      <span className={`w-1.5 h-1.5 rounded-full ${STATUS_DOT[status]}`} aria-hidden="true" />
      <span className={STATUS_TEXT[status]}>{STATUS_LABEL[status]}</span>
    </span>
  );
}

export function EquipmentTab({ inventoryId, canManage }: { inventoryId: string; canManage: boolean }) {
  const [stacks, setStacks] = useState<EquipmentStackResponse[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [units, setUnits] = useState<EquipmentResponse[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [reference, setReference] = useState('');
  const [informations, setInformations] = useState('');
  const [creating, setCreating] = useState(false);

  async function refresh() {
    try {
      setStacks(await listEquipmentGrouped(inventoryId));
      setError(null);
    } catch {
      setError("Impossible de charger l'équipement.");
    }
  }

  useEffect(() => {
    refresh();
  }, [inventoryId]);

  async function toggleExpand(stackKey: string, unitIds: string[]) {
    if (expanded === stackKey) {
      setExpanded(null);
      setUnits([]);
      return;
    }
    const loaded = await Promise.all(unitIds.map((id) => getEquipment(id)));
    setUnits(loaded);
    setExpanded(stackKey);
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setCreating(true);
    try {
      await createEquipment(inventoryId, { name, reference, informations: informations || null });
      setName('');
      setReference('');
      setInformations('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setCreating(false);
    }
  }

  async function handleUpdateStatus(unitId: string, status: EquipmentStatus) {
    try {
      await updateEquipment(unitId, { status });
      if (expanded) {
        const unitIds = units.map((u) => u.id);
        const loaded = await Promise.all(unitIds.map((id) => getEquipment(id)));
        setUnits(loaded);
      }
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleDelete(unitId: string) {
    try {
      await deleteEquipment(unitId);
      setUnits((prev) => prev.filter((u) => u.id !== unitId));
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />

      <ul className="bg-white border border-line rounded-md divide-y divide-line">
        {stacks.map((stack) => {
          const stackKey = `${stack.name}::${stack.reference}`;
          return (
            <li key={stackKey}>
              <button
                onClick={() => toggleExpand(stackKey, stack.unitIds)}
                className="w-full flex items-center justify-between text-left px-4 py-3"
              >
                <span className="font-medium text-ink">
                  {stack.name} <span className="text-gray-500 font-mono text-sm font-normal">{stack.reference}</span>
                </span>
                <span className="text-sm text-gray-600">
                  {stack.unitsActive} disponible{stack.unitsActive > 1 ? 's' : ''} / {stack.unitsTotal}
                </span>
              </button>
              {expanded === stackKey && (
                <ul className="border-t border-line divide-y divide-line bg-canvas">
                  {units.map((unit) => (
                    <li key={unit.id} className="flex items-center justify-between text-sm px-4 py-2.5">
                      <span className="flex items-center gap-3">
                        <StatusBadge status={unit.status} />
                        {unit.informations && <span className="text-gray-500">— {unit.informations}</span>}
                      </span>
                      {canManage && (
                        <span className="flex gap-2 items-center">
                          <select
                            value={unit.status}
                            onChange={(e) => handleUpdateStatus(unit.id, e.target.value as EquipmentStatus)}
                            className="border border-line rounded-md bg-white px-2 py-1 text-xs text-ink"
                          >
                            {(['Active', 'Maintenance', 'Retired'] as EquipmentStatus[]).map((s) => (
                              <option key={s} value={s}>{STATUS_LABEL[s]}</option>
                            ))}
                          </select>
                          <Button variant="danger" onClick={() => handleDelete(unit.id)}>Supprimer</Button>
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </li>
          );
        })}
        {stacks.length === 0 && <li className="px-4 py-3 text-sm text-gray-600">Aucun équipement pour le moment.</li>}
      </ul>

      {canManage && (
        <form onSubmit={handleCreate} className="bg-white border border-line rounded-md p-4 space-y-3 max-w-md">
          <h3 className="text-sm font-semibold text-ink">Ajouter un équipement</h3>
          <TextField label="Nom" required value={name} onChange={setName} />
          <TextField label="Référence" required value={reference} onChange={setReference} />
          <TextField label="Informations (optionnel)" value={informations} onChange={setInformations} />
          <Button type="submit" disabled={creating}>{creating ? 'Ajout...' : 'Ajouter'}</Button>
        </form>
      )}
    </div>
  );
}
