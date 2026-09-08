import { useEffect, useState, type FormEvent } from 'react';
import { listReservations, createReservation, cancelReservation, type ReservationResponse } from '../../api/reservations';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

function toLocalDateInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function defaultRange() {
  const from = new Date();
  const to = new Date();
  to.setDate(to.getDate() + 30);
  return { from: toLocalDateInput(from), to: toLocalDateInput(to) };
}

export function ReservationsTab({ inventoryId }: { inventoryId: string }) {
  const initial = defaultRange();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [reservations, setReservations] = useState<ReservationResponse[]>([]);
  const [listError, setListError] = useState<string | null>(null);

  const [equipmentName, setEquipmentName] = useState('');
  const [equipmentReference, setEquipmentReference] = useState('');
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function refresh() {
    try {
      const fromIso = new Date(`${from}T00:00:00`).toISOString();
      const toIso = new Date(`${to}T23:59:59`).toISOString();
      setReservations(await listReservations(inventoryId, fromIso, toIso));
      setListError(null);
    } catch {
      setListError('Impossible de charger les réservations, réessayer.');
    }
  }

  useEffect(() => {
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inventoryId, from, to]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setSubmitting(true);
    try {
      await createReservation(inventoryId, {
        name: equipmentName,
        reference: equipmentReference,
        startsAt: new Date(startsAt).toISOString(),
        endsAt: new Date(endsAt).toISOString(),
      });
      setEquipmentName('');
      setEquipmentReference('');
      setStartsAt('');
      setEndsAt('');
      await refresh();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCancel(id: string) {
    try {
      await cancelReservation(id);
      await refresh();
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex gap-4 items-end">
        <TextField label="Du" type="date" value={from} onChange={setFrom} />
        <TextField label="Au" type="date" value={to} onChange={setTo} />
      </div>

      <ErrorText message={listError} />

      <ul className="space-y-2">
        {reservations.map((r) => (
          <li key={r.id} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              <span className="font-medium text-primary-800">{r.equipmentName}</span>{' '}
              <span className="text-gray-500">({r.reference})</span> — {r.userDisplayName}
              <br />
              {new Date(r.startsAt).toLocaleString('fr-FR')} → {new Date(r.endsAt).toLocaleString('fr-FR')}
              {r.status === 'Cancelled' && <span className="text-red-600 ml-2">(annulée)</span>}
            </span>
            {r.status === 'Confirmed' && (
              <Button variant="secondary" onClick={() => handleCancel(r.id)}>Annuler</Button>
            )}
          </li>
        ))}
        {reservations.length === 0 && <p className="text-sm text-gray-600">Aucune réservation sur cette période.</p>}
      </ul>

      <form onSubmit={handleCreate} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
        <h3 className="font-semibold text-primary-800">Réserver un équipement</h3>
        <ErrorText message={formError} />
        <TextField label="Nom de l'équipement" required value={equipmentName} onChange={setEquipmentName} />
        <TextField label="Référence" required value={equipmentReference} onChange={setEquipmentReference} />
        <TextField label="Début" type="datetime-local" required value={startsAt} onChange={setStartsAt} />
        <TextField label="Fin" type="datetime-local" required value={endsAt} onChange={setEndsAt} />
        <Button type="submit" disabled={submitting}>{submitting ? 'Réservation...' : 'Réserver'}</Button>
      </form>
    </div>
  );
}
