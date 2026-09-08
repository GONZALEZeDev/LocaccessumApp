import { apiFetch } from './client';

export type ReservationStatus = 'Confirmed' | 'Cancelled';

export interface ReservationResponse {
  id: string;
  equipmentId: string;
  equipmentName: string;
  reference: string;
  userId: string;
  userDisplayName: string;
  startsAt: string;
  endsAt: string;
  status: ReservationStatus;
}

export function listReservations(
  inventoryId: string,
  from: string,
  to: string,
  equipmentId?: string,
): Promise<ReservationResponse[]> {
  const params = new URLSearchParams({ from, to });
  if (equipmentId) params.set('equipmentId', equipmentId);
  return apiFetch<ReservationResponse[]>(`/api/inventories/${inventoryId}/reservations?${params.toString()}`);
}

export function createReservation(
  inventoryId: string,
  data: { equipmentId?: string; name?: string; reference?: string; startsAt: string; endsAt: string },
): Promise<ReservationResponse> {
  return apiFetch<ReservationResponse>(`/api/inventories/${inventoryId}/reservations`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function cancelReservation(id: string): Promise<ReservationResponse> {
  return apiFetch<ReservationResponse>(`/api/reservations/${id}/cancel`, { method: 'POST' });
}
