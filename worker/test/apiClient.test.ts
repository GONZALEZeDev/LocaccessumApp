import { describe, it, expect, vi, afterEach } from 'vitest';
import { fetchUpcomingReservations, markReminderSent } from '../src/apiClient.js';
import type { WorkerConfig } from '../src/config.js';

const config: WorkerConfig = {
  apiBaseUrl: 'http://localhost:8080',
  internalApiKey: 'test-key',
  smtpHost: 'x', smtpPort: 1, smtpUser: 'x', smtpPass: 'x', mailFrom: 'x@x.io',
  reminderWindowHours: 24,
  cronSchedule: '0 * * * *',
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('fetchUpcomingReservations', () => {
  it('calls the correct URL with the API key header and returns the parsed list', async () => {
    const sample = [{
      reservationId: 'r1', userEmail: 'a@x.io', userDisplayName: 'A',
      equipmentName: 'Cam', reference: 'C1', inventoryName: 'Inv',
      startsAt: '2026-01-01T00:00:00Z', endsAt: '2026-01-01T02:00:00Z',
    }];
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, statusText: 'OK', json: async () => sample });
    vi.stubGlobal('fetch', fetchMock);

    const result = await fetchUpcomingReservations(config);

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:8080/api/internal/reservations/upcoming?windowHours=24',
      { headers: { 'X-Internal-Api-Key': 'test-key' } },
    );
    expect(result).toEqual(sample);
  });

  it('throws a descriptive error on a non-OK response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 401, statusText: 'Unauthorized' }));
    await expect(fetchUpcomingReservations(config)).rejects.toThrow(/401/);
  });
});

describe('markReminderSent', () => {
  it('POSTs to the reminder-sent endpoint with the API key header', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204, statusText: 'No Content' });
    vi.stubGlobal('fetch', fetchMock);

    await markReminderSent(config, 'r1');

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:8080/api/internal/reservations/r1/reminder-sent',
      { method: 'POST', headers: { 'X-Internal-Api-Key': 'test-key' } },
    );
  });

  it('throws a descriptive error on a non-OK response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 404, statusText: 'Not Found' }));
    await expect(markReminderSent(config, 'unknown')).rejects.toThrow(/404/);
  });
});
