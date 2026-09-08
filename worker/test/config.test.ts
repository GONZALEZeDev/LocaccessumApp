import { describe, it, expect } from 'vitest';
import { loadConfig } from '../src/config.js';

const validEnv = {
  API_BASE_URL: 'http://localhost:8080/',
  INTERNAL_API_KEY: 'test-key',
  SMTP_HOST: 'smtp.example.test',
  SMTP_PORT: '2525',
  SMTP_USER: 'user',
  SMTP_PASS: 'pass',
  MAIL_FROM: 'reminders@example.test',
};

describe('loadConfig', () => {
  it('loads a valid config, strips a trailing slash from apiBaseUrl, and applies defaults', () => {
    const config = loadConfig(validEnv);
    expect(config.apiBaseUrl).toBe('http://localhost:8080');
    expect(config.smtpPort).toBe(2525);
    expect(config.reminderWindowHours).toBe(24);
    expect(config.cronSchedule).toBe('0 * * * *');
  });

  it('throws naming the missing variable when a required one is absent', () => {
    const { API_BASE_URL, ...rest } = validEnv;
    expect(() => loadConfig(rest)).toThrow(/API_BASE_URL/);
  });

  it('respects REMINDER_WINDOW_HOURS and CRON_SCHEDULE overrides', () => {
    const config = loadConfig({ ...validEnv, REMINDER_WINDOW_HOURS: '48', CRON_SCHEDULE: '*/30 * * * *' });
    expect(config.reminderWindowHours).toBe(48);
    expect(config.cronSchedule).toBe('*/30 * * * *');
  });

  it('throws when SMTP_PORT is not a positive integer', () => {
    expect(() => loadConfig({ ...validEnv, SMTP_PORT: 'not-a-number' })).toThrow(/SMTP_PORT/);
  });

  it('succeeds with SMTP_USER/SMTP_PASS absent from env, defaulting them to an empty string', () => {
    const { SMTP_USER, SMTP_PASS, ...rest } = validEnv;
    const config = loadConfig(rest);
    expect(config.smtpUser).toBe('');
    expect(config.smtpPass).toBe('');
  });

  it('accepts SMTP_USER/SMTP_PASS present but set to an empty string', () => {
    const config = loadConfig({ ...validEnv, SMTP_USER: '', SMTP_PASS: '' });
    expect(config.smtpUser).toBe('');
    expect(config.smtpPass).toBe('');
  });
});
