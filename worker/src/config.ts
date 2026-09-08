export interface WorkerConfig {
  apiBaseUrl: string;
  internalApiKey: string;
  smtpHost: string;
  smtpPort: number;
  smtpUser: string;
  smtpPass: string;
  mailFrom: string;
  reminderWindowHours: number;
  cronSchedule: string;
}

const REQUIRED_KEYS = [
  'API_BASE_URL',
  'INTERNAL_API_KEY',
  'SMTP_HOST',
  'SMTP_PORT',
  'MAIL_FROM',
] as const;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): WorkerConfig {
  const missing = REQUIRED_KEYS.filter((key) => !env[key]);
  if (missing.length > 0) {
    throw new Error(`Missing required environment variable(s): ${missing.join(', ')}`);
  }

  const smtpPort = Number(env.SMTP_PORT);
  if (!Number.isInteger(smtpPort) || smtpPort <= 0) {
    throw new Error(`SMTP_PORT must be a positive integer, got: ${env.SMTP_PORT}`);
  }

  const reminderWindowHours = env.REMINDER_WINDOW_HOURS ? Number(env.REMINDER_WINDOW_HOURS) : 24;
  if (!Number.isInteger(reminderWindowHours) || reminderWindowHours <= 0) {
    throw new Error(`REMINDER_WINDOW_HOURS must be a positive integer, got: ${env.REMINDER_WINDOW_HOURS}`);
  }

  return {
    apiBaseUrl: env.API_BASE_URL!.replace(/\/+$/, ''),
    internalApiKey: env.INTERNAL_API_KEY!,
    smtpHost: env.SMTP_HOST!,
    smtpPort,
    smtpUser: env.SMTP_USER ?? '',
    smtpPass: env.SMTP_PASS ?? '',
    mailFrom: env.MAIL_FROM!,
    reminderWindowHours,
    cronSchedule: env.CRON_SCHEDULE ?? '0 * * * *',
  };
}
