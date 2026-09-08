import type { Transporter } from 'nodemailer';
import type { WorkerConfig } from './config.js';
import { fetchUpcomingReservations, markReminderSent } from './apiClient.js';
import { sendReminderEmail } from './mailer.js';

export interface ReminderJobResult {
  processed: number;
  sent: number;
  failed: number;
}

export async function runReminderJob(config: WorkerConfig, transport: Transporter): Promise<ReminderJobResult> {
  const reservations = await fetchUpcomingReservations(config);
  let sent = 0;
  let failed = 0;

  for (const reservation of reservations) {
    try {
      await sendReminderEmail(transport, config, reservation);
      await markReminderSent(config, reservation.reservationId);
      sent++;
    } catch (err) {
      failed++;
      console.error(`[reminder-job] failed to process reservation ${reservation.reservationId}:`, err);
    }
  }

  return { processed: reservations.length, sent, failed };
}
