# Locaccessum Worker (Plan 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Deviation from the skill's default: no task ends with a `git commit`.** Every implementer stops after its last verification step and leaves changes uncommitted on disk for the user's own review and commit — see Global Constraints.

**Goal:** Build the Node.js reminder worker — an independent service that checks hourly for reservations starting in ~24h and emails a reminder, via the backend's already-built internal endpoint.

**Architecture:** A small TypeScript/Node service with no database access of its own. `node-cron` triggers an hourly job that calls the backend's `GET /api/internal/reservations/upcoming` (API-key authenticated), sends one email per reservation via `nodemailer`, then calls `POST .../reminder-sent` to mark it — idempotently, so a mid-run crash or SMTP outage just retries next hour without double-sending.

**Tech Stack:** Node.js 22+, TypeScript (ESM, `NodeNext` module resolution), `node-cron`, `nodemailer`, Vitest for tests, Docker (multi-stage) for deployment alongside the existing `postgres`/`api` services.

**Spec:** `docs/superpowers/specs/2026-09-08-locaccessum-design.md` (§7 "Worker Node.js (rappels)" — read it alongside this plan).

## Global Constraints

- **No autonomous commits.** No task's implementer runs `git add`/`git commit`. Every task's final step is "stop here — leave the changes on disk, report status, wait for the user to review and commit before the next task starts." This is a deliberate, explicit deviation from the writing-plans/subagent-driven-development skills' normal template.
- **Node.js 22+**, TypeScript, ESM (`"type": "module"`, relative imports use `.js` extensions per TS-ESM convention even though the source files are `.ts`).
- **No direct database access from the worker, ever.** It only talks to the backend over HTTP, authenticated via the `X-Internal-Api-Key` header.
- **Internal endpoint contract (already built and live in `backend/`, do not modify it):**
  - `GET {API_BASE_URL}/api/internal/reservations/upcoming?windowHours={n}` with header `X-Internal-Api-Key: {key}` → `200` JSON array of `{ reservationId, userEmail, userDisplayName, equipmentName, reference, inventoryName, startsAt, endsAt }` (camelCase, ISO-8601 timestamps), or `401` if the key is missing/wrong.
  - `POST {API_BASE_URL}/api/internal/reservations/{reservationId}/reminder-sent` with the same header → `204` (idempotent — safe to call twice), or `404` if the id doesn't exist.
- **`INTERNAL_API_KEY`** in the worker's config must equal the backend's `InternalApiKey` value — they are two names for the same secret, set independently in each service's env. Note this loudly wherever config is discussed; it's the one cross-service gotcha in this plan.
- **Idempotency is the API's job, not the worker's**: the worker never needs to track "did I already send this" itself — it always asks the API for reservations where `reminder_sent_at IS NULL`, and marks them right after a successful send. A failed send this hour is naturally retried next hour because it was never marked.
- Vitest tests use real `nodemailer` transports where practical (`jsonTransport: true` captures without any network call) rather than mocking `nodemailer` itself — the spec calls for testing via "transport nodemailer en mode capture."
- File-scoped responsibility: one concern per file (`config.ts` reads env, `apiClient.ts` talks HTTP, `mailer.ts` builds/sends email, `reminderJob.ts` orchestrates, `scheduler.ts` wires the cron trigger, `index.ts` is pure composition with no logic of its own to test).

---

## File Structure

```
worker/
├── package.json
├── tsconfig.json
├── vitest.config.ts
├── .env.example
├── .gitignore
├── Dockerfile
├── .dockerignore
├── src/
│   ├── config.ts        # WorkerConfig, loadConfig()
│   ├── apiClient.ts      # UpcomingReservation, fetchUpcomingReservations(), markReminderSent()
│   ├── mailer.ts         # createMailTransport(), buildReminderEmail(), sendReminderEmail()
│   ├── reminderJob.ts    # ReminderJobResult, runReminderJob()
│   ├── scheduler.ts      # CronLike, scheduleReminderJob()
│   └── index.ts          # composition root: loadConfig -> createMailTransport -> scheduleReminderJob
└── test/
    ├── health.test.ts
    ├── config.test.ts
    ├── apiClient.test.ts
    ├── mailer.test.ts
    ├── reminderJob.test.ts
    └── scheduler.test.ts

(repo root, modified)
├── docker-compose.yml            # + worker service
├── .env.example                  # + SMTP_*/MAIL_FROM/REMINDER_WINDOW_HOURS/CRON_SCHEDULE
└── README.md                     # + worker section (FR + EN, matching existing bilingual structure)
```

---

## Task 1: Project scaffold

**Files:**
- Create: `worker/package.json`, `worker/tsconfig.json`, `worker/vitest.config.ts`, `worker/.env.example`, `worker/.gitignore`
- Create: `worker/src/health.ts`
- Test: `worker/test/health.test.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: a working `npm run build` / `npm test` toolchain that every later task depends on.

- [ ] **Step 1: Scaffold the package**

Run from the repo root:
```bash
mkdir -p worker/src worker/test
cd worker
npm init -y
npm install node-cron nodemailer
npm install -D typescript @types/node @types/node-cron @types/nodemailer tsx vitest
```

- [ ] **Step 2: Edit `worker/package.json`**

Set/add these fields (keep whatever `npm init` generated for `name`/`version`, just ensure these are present):
```json
{
  "type": "module",
  "engines": { "node": ">=22" },
  "scripts": {
    "build": "tsc",
    "start": "node dist/index.js",
    "dev": "tsx watch src/index.ts",
    "test": "vitest run"
  }
}
```

- [ ] **Step 3: Write `worker/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "outDir": "dist",
    "rootDir": "src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "sourceMap": true
  },
  "include": ["src/**/*.ts"]
}
```

- [ ] **Step 4: Write `worker/vitest.config.ts`**

```ts
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['test/**/*.test.ts'],
  },
});
```

- [ ] **Step 5: Write `worker/.env.example`**

```
API_BASE_URL=http://localhost:8080
INTERNAL_API_KEY=dev-internal-key-change-me
SMTP_HOST=sandbox.smtp.mailtrap.io
SMTP_PORT=2525
SMTP_USER=
SMTP_PASS=
MAIL_FROM=reminders@locaccessum.dev
REMINDER_WINDOW_HOURS=24
CRON_SCHEDULE=0 * * * *
```
Note in a comment above `INTERNAL_API_KEY`: `# must match the backend's InternalApiKey`.

- [ ] **Step 6: Write `worker/.gitignore`**

```
node_modules/
dist/
.env
```

- [ ] **Step 7: Write the failing test**

`worker/test/health.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { WORKER_NAME } from '../src/health.js';

describe('health', () => {
  it('exports the worker name', () => {
    expect(WORKER_NAME).toBe('locaccessum-reminder-worker');
  });
});
```

- [ ] **Step 8: Run — expect FAIL** (module doesn't exist)

Run: `npm test` (from `worker/`)

- [ ] **Step 9: Implement**

`worker/src/health.ts`:
```ts
export const WORKER_NAME = 'locaccessum-reminder-worker';
```

- [ ] **Step 10: Run — expect PASS, and verify the build too**

Run: `npm test && npm run build` (from `worker/`)
Expected: 1 test passing, `tsc` produces `dist/health.js` with no errors.

- [ ] **Step 11: STOP for review**

Do not commit. Report exactly which files were created, the `npm test`/`npm run build` output, and stop. The user reviews and commits before Task 2 starts.

---

## Task 2: Config loader

**Files:**
- Create: `worker/src/config.ts`
- Test: `worker/test/config.test.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `interface WorkerConfig { apiBaseUrl: string; internalApiKey: string; smtpHost: string; smtpPort: number; smtpUser: string; smtpPass: string; mailFrom: string; reminderWindowHours: number; cronSchedule: string; }` and `function loadConfig(env?: NodeJS.ProcessEnv): WorkerConfig` — used by every later task.

- [ ] **Step 1: Write the failing tests**

`worker/test/config.test.ts`:
```ts
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
});
```

- [ ] **Step 2: Run — expect FAIL**

Run: `npm test` (from `worker/`)

- [ ] **Step 3: Implement**

`worker/src/config.ts`:
```ts
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
  'SMTP_USER',
  'SMTP_PASS',
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
    smtpUser: env.SMTP_USER!,
    smtpPass: env.SMTP_PASS!,
    mailFrom: env.MAIL_FROM!,
    reminderWindowHours,
    cronSchedule: env.CRON_SCHEDULE ?? '0 * * * *',
  };
}
```

- [ ] **Step 4: Run — expect PASS**

Run: `npm test` (from `worker/`)
Expected: 5/5 tests passing (1 pre-existing + 4 new).

- [ ] **Step 5: STOP for review**

Do not commit. Report files changed and test output. Wait for the user.

---

## Task 3: API client

**Files:**
- Create: `worker/src/apiClient.ts`
- Test: `worker/test/apiClient.test.ts`

**Interfaces:**
- Consumes: `WorkerConfig` (Task 2).
- Produces: `interface UpcomingReservation { reservationId: string; userEmail: string; userDisplayName: string; equipmentName: string; reference: string; inventoryName: string; startsAt: string; endsAt: string; }`, `async function fetchUpcomingReservations(config: WorkerConfig): Promise<UpcomingReservation[]>`, `async function markReminderSent(config: WorkerConfig, reservationId: string): Promise<void>` — used by Task 5.

- [ ] **Step 1: Write the failing tests**

`worker/test/apiClient.test.ts`:
```ts
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
```

- [ ] **Step 2: Run — expect FAIL**

Run: `npm test` (from `worker/`)

- [ ] **Step 3: Implement**

`worker/src/apiClient.ts`:
```ts
import type { WorkerConfig } from './config.js';

export interface UpcomingReservation {
  reservationId: string;
  userEmail: string;
  userDisplayName: string;
  equipmentName: string;
  reference: string;
  inventoryName: string;
  startsAt: string;
  endsAt: string;
}

export async function fetchUpcomingReservations(config: WorkerConfig): Promise<UpcomingReservation[]> {
  const url = `${config.apiBaseUrl}/api/internal/reservations/upcoming?windowHours=${config.reminderWindowHours}`;
  const res = await fetch(url, {
    headers: { 'X-Internal-Api-Key': config.internalApiKey },
  });
  if (!res.ok) {
    throw new Error(`fetchUpcomingReservations failed: ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as UpcomingReservation[];
}

export async function markReminderSent(config: WorkerConfig, reservationId: string): Promise<void> {
  const url = `${config.apiBaseUrl}/api/internal/reservations/${reservationId}/reminder-sent`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'X-Internal-Api-Key': config.internalApiKey },
  });
  if (!res.ok) {
    throw new Error(`markReminderSent failed for ${reservationId}: ${res.status} ${res.statusText}`);
  }
}
```

- [ ] **Step 4: Run — expect PASS**

Run: `npm test` (from `worker/`)
Expected: 9/9 tests passing (5 pre-existing + 4 new).

- [ ] **Step 5: STOP for review**

Do not commit. Report files changed and test output. Wait for the user.

---

## Task 4: Mailer

**Files:**
- Create: `worker/src/mailer.ts`
- Test: `worker/test/mailer.test.ts`

**Interfaces:**
- Consumes: `WorkerConfig` (Task 2), `UpcomingReservation` (Task 3).
- Produces: `function createMailTransport(config: WorkerConfig): Transporter`, `function buildReminderEmail(reservation: UpcomingReservation): { subject: string; text: string; html: string }`, `async function sendReminderEmail(transport: Transporter, config: WorkerConfig, reservation: UpcomingReservation): Promise<SentMessageInfo>` (returns nodemailer's send result — needed by tests and harmless to ignore elsewhere) — used by Task 5.

- [ ] **Step 1: Write the failing tests**

`worker/test/mailer.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import nodemailer from 'nodemailer';
import { buildReminderEmail, sendReminderEmail } from '../src/mailer.js';
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
});

describe('sendReminderEmail', () => {
  it('sends via the transport with the correct envelope', async () => {
    const transport = nodemailer.createTransport({ jsonTransport: true });

    const info = await sendReminderEmail(transport, config, reservation);

    const message = JSON.parse(info.message as string);
    expect(message.from).toBe('reminders@locaccessum.dev');
    expect(message.to).toBe('bob@locaccessum.dev');
    expect(message.subject).toContain('Perceuse');
  });
});
```

- [ ] **Step 2: Run — expect FAIL**

Run: `npm test` (from `worker/`)

- [ ] **Step 3: Implement**

`worker/src/mailer.ts`:
```ts
import nodemailer, { type Transporter, type SentMessageInfo } from 'nodemailer';
import type { WorkerConfig } from './config.js';
import type { UpcomingReservation } from './apiClient.js';

export function createMailTransport(config: WorkerConfig): Transporter {
  return nodemailer.createTransport({
    host: config.smtpHost,
    port: config.smtpPort,
    auth: { user: config.smtpUser, pass: config.smtpPass },
  });
}

export function buildReminderEmail(reservation: UpcomingReservation): { subject: string; text: string; html: string } {
  const starts = new Date(reservation.startsAt).toLocaleString('fr-FR');
  const subject = `Rappel : réservation de ${reservation.equipmentName} demain`;
  const text = `Bonjour ${reservation.userDisplayName},\n\nVotre réservation de "${reservation.equipmentName}" (${reservation.reference}) dans l'inventaire "${reservation.inventoryName}" commence le ${starts}.\n\n— Locaccessum`;
  const html = `<p>Bonjour ${reservation.userDisplayName},</p><p>Votre réservation de <strong>${reservation.equipmentName}</strong> (${reservation.reference}) dans l'inventaire <strong>${reservation.inventoryName}</strong> commence le <strong>${starts}</strong>.</p><p>— Locaccessum</p>`;
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
```

- [ ] **Step 4: Run — expect PASS**

Run: `npm test` (from `worker/`)
Expected: 11/11 tests passing (9 pre-existing + 2 new).

- [ ] **Step 5: STOP for review**

Do not commit. Report files changed and test output. Wait for the user.

---

## Task 5: Reminder job orchestration

**Files:**
- Create: `worker/src/reminderJob.ts`
- Test: `worker/test/reminderJob.test.ts`

**Interfaces:**
- Consumes: `WorkerConfig` (Task 2), `fetchUpcomingReservations`/`markReminderSent`/`UpcomingReservation` (Task 3), `sendReminderEmail` (Task 4).
- Produces: `interface ReminderJobResult { processed: number; sent: number; failed: number; }`, `async function runReminderJob(config: WorkerConfig, transport: Transporter): Promise<ReminderJobResult>` — used by Task 6.

- [ ] **Step 1: Write the failing tests**

`worker/test/reminderJob.test.ts`:
```ts
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
```

- [ ] **Step 2: Run — expect FAIL**

Run: `npm test` (from `worker/`)

- [ ] **Step 3: Implement**

`worker/src/reminderJob.ts`:
```ts
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
```

- [ ] **Step 4: Run — expect PASS**

Run: `npm test` (from `worker/`)
Expected: 15/15 tests passing (11 pre-existing + 4 new).

- [ ] **Step 5: STOP for review**

Do not commit. Report files changed and test output. Wait for the user.

---

## Task 6: Scheduler + entry point

**Files:**
- Create: `worker/src/scheduler.ts`
- Create: `worker/src/index.ts`
- Test: `worker/test/scheduler.test.ts`

**Interfaces:**
- Consumes: `WorkerConfig` (Task 2), `runReminderJob` (Task 5), `createMailTransport` (Task 4), `loadConfig` (Task 2).
- Produces: `interface CronLike { schedule(expression: string, callback: () => void | Promise<void>): unknown; }`, `function scheduleReminderJob(config: WorkerConfig, transport: Transporter, cronImpl?: CronLike): void`. `index.ts` is the composition root — no exports, no later task depends on it.

- [ ] **Step 1: Write the failing tests**

`worker/test/scheduler.test.ts`:
```ts
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
```

- [ ] **Step 2: Run — expect FAIL**

Run: `npm test` (from `worker/`)

- [ ] **Step 3: Implement `scheduler.ts`**

`worker/src/scheduler.ts`:
```ts
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
```

- [ ] **Step 4: Write `index.ts`** (composition root, not unit-tested — verified in Task 8's Docker smoke test)

`worker/src/index.ts`:
```ts
import { loadConfig } from './config.js';
import { createMailTransport } from './mailer.js';
import { scheduleReminderJob } from './scheduler.js';

const config = loadConfig();
const transport = createMailTransport(config);

scheduleReminderJob(config, transport);

console.log(`Locaccessum reminder worker started. Schedule: "${config.cronSchedule}", window: ${config.reminderWindowHours}h.`);
```

- [ ] **Step 5: Run — expect PASS, and confirm the build still works**

Run: `npm test && npm run build` (from `worker/`)
Expected: 18/18 tests passing (15 pre-existing + 3 new), `tsc` produces `dist/index.js` with no errors.

- [ ] **Step 6: STOP for review**

Do not commit. Report files changed and test/build output. Wait for the user.

---

## Task 7: Docker integration

**Files:**
- Create: `worker/Dockerfile`, `worker/.dockerignore`
- Modify: `docker-compose.yml` (add `worker` service)
- Modify: `.env.example` (repo root — add SMTP_*/MAIL_FROM/REMINDER_WINDOW_HOURS/CRON_SCHEDULE; `InternalApiKey` already exists there from Plan 1, reuse it)

**Interfaces:**
- Consumes: everything from Tasks 1–6 (the worker must build and run as a container).
- Produces: a `worker` container that starts alongside `postgres`/`api` via `docker compose up`.

- [ ] **Step 1: Write `worker/Dockerfile`**

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY tsconfig.json ./
COPY src ./src
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY package*.json ./
RUN npm ci --omit=dev
COPY --from=build /app/dist ./dist
USER node
CMD ["node", "dist/index.js"]
```

- [ ] **Step 2: Write `worker/.dockerignore`**

```
node_modules
dist
.env
test
*.test.ts
```

- [ ] **Step 3: Add the `worker` service to the root `docker-compose.yml`**

Read the current `docker-compose.yml` first (it has `postgres` and `api` services already). Add a third top-level service under `services:`, alongside them:

```yaml
  worker:
    build:
      context: ./worker
    depends_on:
      api:
        condition: service_started
    environment:
      API_BASE_URL: http://api:8080
      INTERNAL_API_KEY: ${InternalApiKey}
      SMTP_HOST: ${SMTP_HOST}
      SMTP_PORT: ${SMTP_PORT}
      SMTP_USER: ${SMTP_USER}
      SMTP_PASS: ${SMTP_PASS}
      MAIL_FROM: ${MAIL_FROM}
      REMINDER_WINDOW_HOURS: ${REMINDER_WINDOW_HOURS:-24}
      CRON_SCHEDULE: ${CRON_SCHEDULE:-0 * * * *}
    restart: unless-stopped
```
(`API_BASE_URL` uses the Docker network's service name `api`, not `localhost` — the worker container reaches the API container by service name. `INTERNAL_API_KEY` reuses the SAME `InternalApiKey` variable the `api` service already reads — one shared secret, two consumers.)

- [ ] **Step 4: Add the new variables to the root `.env.example`**

Read the current root `.env.example` first (it already has `InternalApiKey`). Append:
```
SMTP_HOST=sandbox.smtp.mailtrap.io
SMTP_PORT=2525
SMTP_USER=
SMTP_PASS=
MAIL_FROM=reminders@locaccessum.dev
REMINDER_WINDOW_HOURS=24
CRON_SCHEDULE=0 * * * *
```

- [ ] **Step 5: Validate the compose config parses**

Run from the repo root: `cp .env.example .env` (only if `.env` doesn't already exist — don't overwrite a real one), then `docker compose config`.
Expected: prints a merged config including all three services, no errors. Do not run `up` yet — that's Task 8.

- [ ] **Step 6: STOP for review**

Do not commit. Report the exact diffs to `docker-compose.yml`/`.env.example` and the `docker compose config` output. Wait for the user.

---

## Task 8: End-to-end validation + README

**Files:**
- Modify: `README.md` (repo root — add a worker section, French then English, matching the existing bilingual structure)
- Test: full worker suite + a real 3-service `docker compose up`

**Interfaces:**
- Consumes: everything.
- Produces: proof that `postgres` + `api` + `worker` genuinely run together, and documentation of it.

- [ ] **Step 1: Run the full worker test suite one more time**

Run: `cd worker && npm test`
Expected: 18/18 passing, 0 failures.

- [ ] **Step 2: Real compose smoke test with all three services**

From the repo root (ensure `.env` exists and has real-looking values — the Mailtrap SMTP fields can stay blank/placeholder for this check, the worker will just fail individual sends harmlessly if no reservations are due anyway):
```bash
docker compose up --build -d
sleep 25
docker compose logs worker
curl -s localhost:8080/health
docker compose down
```
Expected: the `worker` service logs `Locaccessum reminder worker started. Schedule: "0 * * * *", window: 24h.` and does not crash/restart-loop; `/health` still returns `{"status":"ok"}`. If the worker container exits immediately, read the logs to diagnose (most likely cause: a missing/malformed required env var — check `loadConfig`'s error message, which names the missing key).

- [ ] **Step 3: Write the README section**

Add a new section to `README.md`, in BOTH the French part (near the top) and the English part (below the separator), titled "Worker de rappels" / "Reminder worker". Cover: what it does (hourly check, email ~24h before a reservation starts), how to configure it (`cp worker/.env.example worker/.env` for standalone runs, or the root `.env` when using `docker compose up`), that `INTERNAL_API_KEY` must match the backend's `InternalApiKey`, and that SMTP defaults to a Mailtrap sandbox for safe testing (no real emails sent to users during development). Update the top-of-file sentence that currently says "no frontend and no reminder worker yet" — remove the worker half of that claim (the frontend is still not built).

- [ ] **Step 4: STOP for review**

Do not commit. Report the full smoke-test output and the README diff. This is the last task of Plan 3 — once the user reviews and commits, the worker is complete (pending them filling in real Mailtrap credentials in their own `.env`, which was never something Claude needed to see).

---

## Self-Review

**1. Spec coverage (§7):** cron hourly trigger → Task 6. `GET upcoming` with API key + window filter → Task 3. Email send → Task 4. `POST reminder-sent` idempotent mark → Task 3 (client) + Task 5 (orchestration, only called on send success). Failure resilience (log, continue, natural retry next hour since nothing was marked) → Task 5, explicitly tested. Config env vars (`API_BASE_URL, INTERNAL_API_KEY, SMTP_*, MAIL_FROM, REMINDER_WINDOW_HOURS, CRON_SCHEDULE`) → Task 2. Vitest + simulated API + capture-mode nodemailer → Tasks 3/4/5 all follow this. No direct DB access → architecturally true throughout (no task ever imports a DB driver). All of §7 is covered.

**2. Placeholder scan:** no TBD/TODO; every step has real code or an exact command. Task 8 Step 2's `sleep 25` and log-reading is a real, runnable verification, not a hand-wave.

**3. Type consistency:** `WorkerConfig` (Task 2) fields are used identically in every later task's test fixtures (`apiBaseUrl, internalApiKey, smtpHost, smtpPort, smtpUser, smtpPass, mailFrom, reminderWindowHours, cronSchedule` — checked against Tasks 3–6, consistent). `UpcomingReservation` (Task 3) fields match what Task 4/5's fixtures construct. `sendReminderEmail`'s return type (`Promise<SentMessageInfo>`, changed from a bare `void` during drafting so Task 4's test can inspect what was sent) is awaited-and-ignored in Task 5 — compiles fine, no consumer depends on `void`. `ReminderJobResult` (Task 5) matches what Task 6's scheduler logs (`processed/sent/failed`). `CronLike` (Task 6) is a minimal structural interface `node-cron`'s real export satisfies without modification — verified by using the real `cron` import as the default parameter value.

Plan ready.
