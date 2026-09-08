import { describe, it, expect, vi } from 'vitest';
import { scheduleReminderJob, type CronLike } from '../src/scheduler.js';
import * as reminderJobModule from '../src/reminderJob.js';
import type { WorkerConfig } from '../src/config.js';

vi.mock('../src/reminderJob.js');

const config: WorkerConfig = {
  apiBaseUrl: 'http://localhost:8080', internalApiKey: 'k',
  smtpHost: 'x', smtpPort: 1, smtpUser: 'x', smtpPass: 'x', mailFrom: 'x@x.io',
  reminderWindowHours: 24, cronSchedule: '*/15 * * * *',
};

const fakeTransport = {} as any;

describe('scheduleReminderJob', () => {
  it('schedules the job with the configured cron expression', () => {
    const scheduleFn = vi.fn();
    const fakeCron: CronLike = { schedule: scheduleFn };

    scheduleReminderJob(config, fakeTransport, fakeCron);

    expect(scheduleFn).toHaveBeenCalledWith('*/15 * * * *', expect.any(Function));
  });

  it('runs the reminder job when the scheduled callback fires', async () => {
    vi.mocked(reminderJobModule.runReminderJob).mockResolvedValue({ processed: 1, sent: 1, failed: 0 });
    let capturedCallback: () => Promise<void> = async () => {};
    const fakeCron: CronLike = { schedule: (_expr, cb) => { capturedCallback = cb as () => Promise<void>; } };

    scheduleReminderJob(config, fakeTransport, fakeCron);
    await capturedCallback();

    expect(reminderJobModule.runReminderJob).toHaveBeenCalledWith(config, fakeTransport);
  });

  it('does not throw when the reminder job itself rejects', async () => {
    vi.mocked(reminderJobModule.runReminderJob).mockRejectedValue(new Error('boom'));
    let capturedCallback: () => Promise<void> = async () => {};
    const fakeCron: CronLike = { schedule: (_expr, cb) => { capturedCallback = cb as () => Promise<void>; } };

    scheduleReminderJob(config, fakeTransport, fakeCron);

    await expect(capturedCallback()).resolves.toBeUndefined();
  });
});
