import { describe, it, expect, vi, beforeEach } from 'vitest';
import { runReminderJob } from '../src/reminderJob.js';
import * as apiClient from '../src/apiClient.js';
import * as mailer from '../src/mailer.js';
import type { WorkerConfig } from '../src/config.js';
import type { UpcomingReservation } from '../src/apiClient.js';

vi.mock('../src/apiClient.js');
vi.mock('../src/mailer.js');

const config: WorkerConfig = {
  apiBaseUrl: 'http://localhost:8080', internalApiKey: 'k',
  smtpHost: 'x', smtpPort: 1, smtpUser: 'x', smtpPass: 'x', mailFrom: 'x@x.io',
  reminderWindowHours: 24, cronSchedule: '0 * * * *',
};

const fakeTransport = {} as any;

function reservation(id: string): UpcomingReservation {
  return {
    reservationId: id, userEmail: `${id}@x.io`, userDisplayName: id,
    equipmentName: 'Eq', reference: 'R', inventoryName: 'Inv',
    startsAt: '2026-01-01T00:00:00Z', endsAt: '2026-01-01T02:00:00Z',
  };
}

beforeEach(() => {
  vi.resetAllMocks();
});

describe('runReminderJob', () => {
  it('sends and marks every reservation returned, reporting accurate counts', async () => {
    vi.mocked(apiClient.fetchUpcomingReservations).mockResolvedValue([reservation('r1'), reservation('r2')]);
    vi.mocked(mailer.sendReminderEmail).mockResolvedValue({} as any);
    vi.mocked(apiClient.markReminderSent).mockResolvedValue(undefined);

    const result = await runReminderJob(config, fakeTransport);

    expect(result).toEqual({ processed: 2, sent: 2, failed: 0 });
    expect(apiClient.markReminderSent).toHaveBeenCalledWith(config, 'r1');
    expect(apiClient.markReminderSent).toHaveBeenCalledWith(config, 'r2');
  });

  it('continues processing the rest when one reservation fails to send', async () => {
    vi.mocked(apiClient.fetchUpcomingReservations).mockResolvedValue([reservation('r1'), reservation('r2')]);
    vi.mocked(mailer.sendReminderEmail)
      .mockRejectedValueOnce(new Error('SMTP down'))
      .mockResolvedValueOnce({} as any);
    vi.mocked(apiClient.markReminderSent).mockResolvedValue(undefined);

    const result = await runReminderJob(config, fakeTransport);

    expect(result).toEqual({ processed: 2, sent: 1, failed: 1 });
    expect(apiClient.markReminderSent).toHaveBeenCalledTimes(1);
    expect(apiClient.markReminderSent).toHaveBeenCalledWith(config, 'r2');
  });

  it('does not mark reminder-sent when the email send fails', async () => {
    vi.mocked(apiClient.fetchUpcomingReservations).mockResolvedValue([reservation('r1')]);
    vi.mocked(mailer.sendReminderEmail).mockRejectedValue(new Error('SMTP down'));

    const result = await runReminderJob(config, fakeTransport);

    expect(result).toEqual({ processed: 1, sent: 0, failed: 1 });
    expect(apiClient.markReminderSent).not.toHaveBeenCalled();
  });

  it('returns zero counts when there is nothing to remind', async () => {
    vi.mocked(apiClient.fetchUpcomingReservations).mockResolvedValue([]);

    const result = await runReminderJob(config, fakeTransport);

    expect(result).toEqual({ processed: 0, sent: 0, failed: 0 });
  });
});
