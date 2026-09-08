import nodemailer, { type Transporter, type SentMessageInfo, type SMTPTransportOptions } from 'nodemailer';
import type { WorkerConfig } from './config.js';
import type { UpcomingReservation } from './apiClient.js';

export function createMailTransport(config: WorkerConfig): Transporter {
  const transportConfig: SMTPTransportOptions = {
    host: config.smtpHost,
    port: config.smtpPort,
  };
  if (config.smtpUser) {
    transportConfig.auth = { user: config.smtpUser, pass: config.smtpPass };
  }
  return nodemailer.createTransport(transportConfig);
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

export function buildReminderEmail(reservation: UpcomingReservation): { subject: string; text: string; html: string } {
  const starts = new Date(reservation.startsAt).toLocaleString('fr-FR');
  const subject = `Rappel : réservation de ${reservation.equipmentName} demain`;
  const text = `Bonjour ${reservation.userDisplayName},\n\nVotre réservation de "${reservation.equipmentName}" (${reservation.reference}) dans l'inventaire "${reservation.inventoryName}" commence le ${starts}.\n\n— Locaccessum`;
  const html = `<p>Bonjour ${escapeHtml(reservation.userDisplayName)},</p><p>Votre réservation de <strong>${escapeHtml(reservation.equipmentName)}</strong> (${escapeHtml(reservation.reference)}) dans l'inventaire <strong>${escapeHtml(reservation.inventoryName)}</strong> commence le <strong>${starts}</strong>.</p><p>— Locaccessum</p>`;
  return { subject, text, html };
}

export async function sendReminderEmail(
  transport: Transporter,
  config: WorkerConfig,
  reservation: UpcomingReservation,
): Promise<SentMessageInfo> {
  const { subject, text, html } = buildReminderEmail(reservation);
  return transport.sendMail({
    from: config.mailFrom,
    to: reservation.userEmail,
    subject,
    text,
    html,
  });
}
