import cron from 'node-cron';
import type { Transporter } from 'nodemailer';
import type { WorkerConfig } from './config.js';
import { runReminderJob } from './reminderJob.js';

export interface CronLike {
  schedule(expression: string, callback: () => void | Promise<void>): unknown;
}

export function scheduleReminderJob(config: WorkerConfig, transport: Transporter, cronImpl: CronLike = cron): void {
  cronImpl.schedule(config.cronSchedule, async () => {
    try {
      const result = await runReminderJob(config, transport);
      console.log(`[reminder-job] processed=${result.processed} sent=${result.sent} failed=${result.failed}`);
    } catch (err) {
      console.error('[reminder-job] unexpected failure', err);
    }
  });
}
