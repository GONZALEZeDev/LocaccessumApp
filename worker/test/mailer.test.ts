import { describe, it, expect } from 'vitest';
import nodemailer from 'nodemailer';
import { buildReminderEmail, createMailTransport, sendReminderEmail } from '../src/mailer.js';
import type { WorkerConfig } from '../src/config.js';
import type { UpcomingReservation } from '../src/apiClient.js';

const config: WorkerConfig = {
  apiBaseUrl: 'http://localhost:8080', internalApiKey: 'k',
  smtpHost: 'x', smtpPort: 1, smtpUser: 'x', smtpPass: 'x',
  mailFrom: 'reminders@locaccessum.dev',
  reminderWindowHours: 24, cronSchedule: '0 * * * *',
};

const reservation: UpcomingReservation = {
  reservationId: 'r1', userEmail: 'bob@locaccessum.dev', userDisplayName: 'Bob',
  equipmentName: 'Perceuse', reference: 'BOSCH-GSB', inventoryName: 'Atelier Démo',
  startsAt: '2026-01-02T10:00:00.000Z', endsAt: '2026-01-02T12:00:00.000Z',
};

describe('buildReminderEmail', () => {
  it('includes the equipment, reference, inventory and user name', () => {
    const { subject, text, html } = buildReminderEmail(reservation);
    expect(subject).toContain('Perceuse');
    expect(text).toContain('Bob');
    expect(text).toContain('BOSCH-GSB');
    expect(html).toContain('Atelier Démo');
  });

  it('HTML-escapes interpolated fields to prevent HTML/script injection', () => {
    const malicious: UpcomingReservation = {
      ...reservation,
      userDisplayName: '<script>alert(1)</script>',
      equipmentName: 'Foo & Bar <b>x</b>',
      reference: '"onmouseover="alert(1)',
      inventoryName: "O'Brien's <img src=x onerror=alert(1)>",
    };
    const { html } = buildReminderEmail(malicious);
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;script&gt;');
    expect(html).toContain('&amp;');
    expect(html).toContain('&quot;onmouseover=&quot;');
    expect(html).toContain('&#39;Brien&#39;s');
  });
});

describe('createMailTransport', () => {
  it('omits the auth option entirely when smtpUser is empty', () => {
    const transport = createMailTransport({ ...config, smtpUser: '', smtpPass: '' });
    expect(transport.options).not.toHaveProperty('auth');
  });

  it('includes the auth option when smtpUser is set', () => {
    const transport = createMailTransport({ ...config, smtpUser: 'user', smtpPass: 'pass' });
    expect(transport.options).toMatchObject({ auth: { user: 'user', pass: 'pass' } });
  });
});

describe('sendReminderEmail', () => {
  it('sends via the transport with the correct envelope', async () => {
    const transport = nodemailer.createTransport({ jsonTransport: true });

    const info = await sendReminderEmail(transport, config, reservation);

    const message = JSON.parse(info.message as string);
    expect(message.from.address).toBe('reminders@locaccessum.dev');
    expect(message.to[0].address).toBe('bob@locaccessum.dev');
    expect(message.subject).toContain('Perceuse');
  });
});
