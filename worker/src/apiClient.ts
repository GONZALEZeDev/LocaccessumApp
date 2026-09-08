import type { WorkerConfig } from './config.js';

export interface UpcomingReservation {
  reservationId: string;
  userEmail: string;
  userDisplayName: string;
  equipmentName: string;
  reference: string;
  inventoryName: string;
  startsAt: string;
  endsAt: string;
}

export async function fetchUpcomingReservations(config: WorkerConfig): Promise<UpcomingReservation[]> {
  const url = `${config.apiBaseUrl}/api/internal/reservations/upcoming?windowHours=${config.reminderWindowHours}`;
  const res = await fetch(url, {
    headers: { 'X-Internal-Api-Key': config.internalApiKey },
  });
  if (!res.ok) {
    throw new Error(`fetchUpcomingReservations failed: ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as UpcomingReservation[];
}

export async function markReminderSent(config: WorkerConfig, reservationId: string): Promise<void> {
  const url = `${config.apiBaseUrl}/api/internal/reservations/${reservationId}/reminder-sent`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'X-Internal-Api-Key': config.internalApiKey },
  });
  if (!res.ok) {
    throw new Error(`markReminderSent failed for ${reservationId}: ${res.status} ${res.statusText}`);
  }
}
