import { loadConfig } from './config.js';
import { createMailTransport } from './mailer.js';
import { scheduleReminderJob } from './scheduler.js';

const config = loadConfig();
const transport = createMailTransport(config);

scheduleReminderJob(config, transport);

console.log(`Locaccessum reminder worker started. Schedule: "${config.cronSchedule}", window: ${config.reminderWindowHours}h.`);
