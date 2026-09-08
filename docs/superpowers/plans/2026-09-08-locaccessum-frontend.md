# Locaccessum Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A simple, functional React frontend covering every existing backend feature (auth, inventories, equipment/stacking, reservations, members, invitations) with minimal visual design (pastel green, Tailwind), reduced automated-test scope, but strict security requirements.

**Architecture:** Vite + React + TypeScript SPA, React Router for client-side routing, Tailwind CSS v4 for styling, a thin typed `fetch` wrapper per backend resource (no React Query/state library), JWT in `localStorage`.

**Tech Stack:** React 19, react-router-dom 7, Tailwind CSS v4 (`@tailwindcss/vite`), Vite, TypeScript, Vitest + `@testing-library/react` (targeted tests only — see Global Constraints).

**Spec:** `docs/superpowers/specs/2026-09-08-locaccessum-frontend-design.md` (this plan's binding design authority) and `docs/superpowers/specs/2026-09-08-locaccessum-design.md` (master spec).

## Global Constraints

- **No backend changes.** The backend (Plan 1) is done and untouched by this plan.
- **No `dangerouslySetInnerHTML`, no `eval`, no `new Function`.** All dynamic text renders through normal JSX interpolation (auto-escaped by React).
- **JWT lives under exactly one `localStorage` key**, `locaccessum_token`, accessed only through `src/api/client.ts`'s `getToken`/`setToken`/`clearToken` — no other file touches `localStorage` for the token directly.
- **Frontend role-based UI hiding is UX only, never enforcement.** Never introduce client-side-only permission logic that could be mistaken for real access control — the backend is the sole authority.
- **Error messages shown to the user come only from the backend's `detail`/`errors`/`title` fields** (all documented as safe/user-facing in the DTO reference below) — never a raw exception, stack trace, or `traceId`.
- **No state management library** (Redux/Zustand) — one `AuthContext` for the current user is the only global state.
- **No React Query/SWR** — plain `fetch` via `src/api/client.ts`.
- **Reduced automated-test scope, by design:** only `src/api/client.ts` (Task 2) and `src/auth/AuthContext.tsx` (Task 3) get automated tests. No other component/page is unit-tested — verified instead by a real manual click-through in Task 8.
- **PROCESS OVERRIDE (same as Plans 1/3): no autonomous commits.** No task's implementer runs `git commit` — every task stops after implementation with changes left uncommitted on disk, for the user to review and commit themselves before the next task starts. Task reviews are built from the uncommitted diff (`git add -A && git diff --cached -U10 > file && git status --porcelain=v1 >> file && git reset`), never from a commit range.
- **Reduced review depth, by design:** each task gets one review pass on a cheap/mid-tier model; fix loop only for Critical/Important findings (Minor findings are deferred, never looped). The final whole-plan review is the one place full rigor applies, and it is explicitly security-focused (see Task 8).

## Backend DTO Reference (all fields camelCase JSON except where noted; all enum values are PascalCase strings)

```
AuthResponse       { token, userId, email, displayName, userCode }
UserResponse       { userId, email, displayName, userCode, createdAt }
UserSummaryResponse{ userId, displayName, userCode }

InventoryResponse         { id, name, description, ownerId, createdAt, myRole: "Owner"|"Admin"|"Member" }
InventoryListItemResponse { id, name, description, myRole, memberCount }

EquipmentResponse      { id, inventoryId, name, reference, informations, status: "Active"|"Maintenance"|"Retired", createdAt }
EquipmentStackResponse { name, reference, unitsTotal, unitsActive, unitsMaintenance, unitsRetired, unitIds: string[] }

ReservationResponse { id, equipmentId, equipmentName, reference, userId, userDisplayName, startsAt, endsAt, status: "Confirmed"|"Cancelled" }

InvitationResponse { id, inventoryId, inventoryName, invitedUserId, invitedByUserId, role: "Admin"|"Member", status: "Pending"|"Accepted"|"Declined"|"Revoked", createdAt }

MemberResponse { userId, displayName, userCode, role: "Owner"|"Admin"|"Member", joinedAt }
```

Error responses (all possible shapes, `Content-Type: application/problem+json` where a body exists):
- `{ status, detail, code, title, type, instance }` — most business-rule errors; `detail` is safe to display, `code` for branching.
- `{ status, title, errors: { FieldName: ["message"] } }` — ASP.NET model validation (400); `errors` values are safe to display.
- Bare `401`/`403` with **no JSON body** for auth/policy failures — never try to parse these, treat as "redirect to /login" (401) or "show a generic access-denied message" (403).
- `{ status, title, traceId }` (500, no `detail`) — always show a generic message, never `title`/`traceId` verbatim if traceId is present (it's an opaque id, safe to show, but not informative — a generic message is friendlier).

## Task 1: Scaffold

**Files:**
- Create: `frontend/package.json`, `frontend/tsconfig.json`, `frontend/tsconfig.node.json`, `frontend/vite.config.ts`, `frontend/vitest.config.ts`, `frontend/index.html`, `frontend/.env.example`, `frontend/.gitignore`
- Create: `frontend/src/main.tsx`, `frontend/src/App.tsx`, `frontend/src/index.css`, `frontend/src/vite-env.d.ts`

**Interfaces:**
- Produces: a running Vite dev server on port 5173, Tailwind v4 pastel-green theme available as `primary-{50..900}` utility classes, a placeholder route so later tasks have something to replace.

- [ ] **Step 1: `frontend/package.json`**

```json
{
  "name": "frontend",
  "version": "1.0.0",
  "type": "module",
  "engines": { "node": ">=22" },
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test": "vitest run"
  },
  "dependencies": {
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "react-router-dom": "^7.0.0"
  },
  "devDependencies": {
    "@testing-library/jest-dom": "^6.0.0",
    "@testing-library/react": "^16.0.0",
    "@tailwindcss/vite": "^4.0.0",
    "@types/react": "^19.0.0",
    "@types/react-dom": "^19.0.0",
    "@vitejs/plugin-react": "^4.0.0",
    "jsdom": "^25.0.0",
    "tailwindcss": "^4.0.0",
    "typescript": "^5.6.0",
    "vite": "^6.0.0",
    "vitest": "^5.0.0"
  }
}
```

- [ ] **Step 2: `frontend/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "jsx": "react-jsx",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["vite/client"]
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

- [ ] **Step 3: `frontend/tsconfig.node.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "strict": true,
    "types": ["node"],
    "noEmit": true
  },
  "include": ["vite.config.ts", "vitest.config.ts"]
}
```

- [ ] **Step 4: `frontend/vite.config.ts`**

```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { port: 5173 },
});
```

- [ ] **Step 5: `frontend/vitest.config.ts`**

```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: [],
  },
});
```

- [ ] **Step 6: `frontend/index.html`**

```html
<!doctype html>
<html lang="fr">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Locaccessum</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 7: `frontend/src/index.css`**

```css
@import "tailwindcss";

@theme {
  --color-primary-50: #f4faf3;
  --color-primary-100: #e6f5e3;
  --color-primary-200: #cceac6;
  --color-primary-300: #a8d9a0;
  --color-primary-400: #8ac77e;
  --color-primary-500: #6fb161;
  --color-primary-600: #5a9750;
  --color-primary-700: #487a40;
  --color-primary-800: #3a6234;
  --color-primary-900: #2f4f2a;
}

body {
  margin: 0;
}
```

- [ ] **Step 8: `frontend/src/vite-env.d.ts`**

```ts
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
```

- [ ] **Step 9: `frontend/src/App.tsx`** (placeholder — replaced by Task 3's real routing)

```tsx
export default function App() {
  return (
    <div className="min-h-screen bg-primary-50 flex items-center justify-center">
      <p className="text-primary-800 text-lg">Locaccessum — frontend scaffold OK.</p>
    </div>
  );
}
```

- [ ] **Step 10: `frontend/src/main.tsx`**

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

- [ ] **Step 11: `frontend/.env.example`**

```
VITE_API_BASE_URL=http://localhost:8080
```

- [ ] **Step 12: `frontend/.gitignore`**

```
node_modules
dist
.env
```

- [ ] **Step 13: Install, build, and verify**

Run from `frontend/`: `npm install && npm run build`.
Expected: build succeeds, produces `dist/index.html` + assets.

**Ruling carried into this task (verify and adapt if wrong, don't guess):** this step assumes `npm install` resolves Tailwind CSS to a v4.x release using the `@tailwindcss/vite` plugin + CSS-first `@theme` config (no separate `tailwind.config.ts` needed). After `npm install`, check the resolved version (`npm ls tailwindcss`). If it resolved to a major version other than 4, or `@theme`-based colors don't produce `bg-primary-500`/`text-primary-700` etc. utility classes when used, you are authorized to adapt (e.g. add a classic `tailwind.config.ts` with a `theme.extend.colors.primary` palette using the same hex values listed in Step 7, and adjust `vite.config.ts`/`src/index.css` accordingly) — note exactly what you changed and why in your report. Verify by adding `<div className="bg-primary-100 text-primary-700 p-2">test</div>` temporarily inside Step 9's placeholder, running `npm run dev`, and confirming in a browser (or by inspecting the built CSS in `dist/assets/*.css` for the expected color values) that the utility actually applies pastel-green styling — then leave Step 9's placeholder as written above (remove the temporary test div) once confirmed.

- [ ] **Step 14: STOP for review**

Do not commit. Report exactly what was created, the build output, and how Tailwind's theme was verified (and whether the ruling above was invoked). Wait for the user.

---

## Task 2: API client

**Files:**
- Create: `frontend/src/api/client.ts`, `frontend/src/api/auth.ts`, `frontend/src/api/users.ts`, `frontend/src/api/inventories.ts`, `frontend/src/api/equipment.ts`, `frontend/src/api/reservations.ts`, `frontend/src/api/invitations.ts`, `frontend/src/api/members.ts`
- Test: `frontend/src/api/client.test.ts`

**Interfaces:**
- Consumes: nothing from earlier tasks (this is the foundation layer).
- Produces: `ApiError`, `getToken`/`setToken`/`clearToken`, `apiFetch<T>`, and one typed function per backend endpoint (used by every later task) — exact exports listed in each file below.

- [ ] **Step 1: `frontend/src/api/client.ts`**

```ts
const TOKEN_KEY = 'locaccessum_token';
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(status: number, message: string, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> | undefined),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });

  if (res.status === 204) {
    return undefined as T;
  }

  if (!res.ok) {
    let message = `Une erreur est survenue (${res.status}).`;
    let code: string | undefined;
    try {
      const body = await res.json();
      if (typeof body?.detail === 'string') {
        message = body.detail;
      } else if (body?.errors && typeof body.errors === 'object') {
        const firstField = Object.keys(body.errors)[0];
        const firstMessage = firstField ? body.errors[firstField]?.[0] : undefined;
        message = typeof firstMessage === 'string' ? firstMessage : (body.title ?? message);
      } else if (typeof body?.title === 'string') {
        message = body.title;
      }
      if (typeof body?.code === 'string') code = body.code;
    } catch {
      // No JSON body (e.g. bare 401/403 from the auth/policy layer) — keep the generic message.
    }
    throw new ApiError(res.status, message, code);
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
```

- [ ] **Step 2: `frontend/src/api/client.test.ts`**

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { apiFetch, ApiError, getToken, setToken, clearToken } from './client';

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('token helpers', () => {
  it('round-trips a token through localStorage under one key', () => {
    expect(getToken()).toBeNull();
    setToken('abc123');
    expect(getToken()).toBe('abc123');
    clearToken();
    expect(getToken()).toBeNull();
  });
});

describe('apiFetch', () => {
  it('does not send an Authorization header when no token is stored', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, text: async () => '{"ok":true}' });
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch('/api/whatever');

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers.Authorization).toBeUndefined();
  });

  it('sends the Bearer token from localStorage when present', async () => {
    setToken('abc123');
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, text: async () => '{"ok":true}' });
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch('/api/whatever');

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers.Authorization).toBe('Bearer abc123');
  });

  it('returns undefined for a 204 No Content response', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204, text: async () => '' });
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiFetch('/api/whatever', { method: 'DELETE' });

    expect(result).toBeUndefined();
  });

  it('throws an ApiError using the "detail" field when present', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      json: async () => ({ title: 'Not Found', status: 404, detail: 'Inventory not found.', code: 'INVENTORY_NOT_FOUND' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(apiFetch('/api/inventories/x')).rejects.toMatchObject({
      message: 'Inventory not found.',
      status: 404,
      code: 'INVENTORY_NOT_FOUND',
    });
  });

  it('throws an ApiError using the first validation "errors" message when there is no "detail"', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Email: ['The Email field is not a valid e-mail address.'] },
      }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(apiFetch('/api/auth/register')).rejects.toMatchObject({
      message: 'The Email field is not a valid e-mail address.',
      status: 400,
    });
  });

  it('falls back to a generic message when the response has no JSON body (e.g. bare 401)', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => {
        throw new Error('no body');
      },
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(apiFetch('/api/users/me')).rejects.toBeInstanceOf(ApiError);
  });
});
```

- [ ] **Step 3: `frontend/src/api/auth.ts`**

```ts
import { apiFetch } from './client';

export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  displayName: string;
  userCode: string;
}

export function register(email: string, password: string, displayName: string): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password, displayName }),
  });
}

export function login(email: string, password: string): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
}
```

- [ ] **Step 4: `frontend/src/api/users.ts`**

```ts
import { apiFetch } from './client';

export interface UserResponse {
  userId: string;
  email: string;
  displayName: string;
  userCode: string;
  createdAt: string;
}

export interface UserSummaryResponse {
  userId: string;
  displayName: string;
  userCode: string;
}

export function getMe(): Promise<UserResponse> {
  return apiFetch<UserResponse>('/api/users/me');
}

export function searchUserByCode(code: string): Promise<UserSummaryResponse> {
  return apiFetch<UserSummaryResponse>(`/api/users/search?code=${encodeURIComponent(code)}`);
}
```

- [ ] **Step 5: `frontend/src/api/inventories.ts`**

```ts
import { apiFetch } from './client';

export type MembershipRole = 'Owner' | 'Admin' | 'Member';

export interface InventoryResponse {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  createdAt: string;
  myRole: MembershipRole;
}

export interface InventoryListItemResponse {
  id: string;
  name: string;
  description: string | null;
  myRole: MembershipRole;
  memberCount: number;
}

export function listInventories(): Promise<InventoryListItemResponse[]> {
  return apiFetch<InventoryListItemResponse[]>('/api/inventories');
}

export function getInventory(id: string): Promise<InventoryResponse> {
  return apiFetch<InventoryResponse>(`/api/inventories/${id}`);
}

export function createInventory(name: string, description: string | null): Promise<InventoryResponse> {
  return apiFetch<InventoryResponse>('/api/inventories', {
    method: 'POST',
    body: JSON.stringify({ name, description }),
  });
}

export function updateInventory(id: string, name: string | null, description: string | null): Promise<InventoryResponse> {
  return apiFetch<InventoryResponse>(`/api/inventories/${id}`, {
    method: 'PATCH',
    body: JSON.stringify({ name, description }),
  });
}

export function deleteInventory(id: string): Promise<void> {
  return apiFetch<void>(`/api/inventories/${id}`, { method: 'DELETE' });
}

export function transferOwnership(id: string, newOwnerUserId: string): Promise<void> {
  return apiFetch<void>(`/api/inventories/${id}/transfer-ownership`, {
    method: 'POST',
    body: JSON.stringify({ newOwnerUserId }),
  });
}
```

- [ ] **Step 6: `frontend/src/api/equipment.ts`**

```ts
import { apiFetch } from './client';

export type EquipmentStatus = 'Active' | 'Maintenance' | 'Retired';

export interface EquipmentResponse {
  id: string;
  inventoryId: string;
  name: string;
  reference: string;
  informations: string | null;
  status: EquipmentStatus;
  createdAt: string;
}

export interface EquipmentStackResponse {
  name: string;
  reference: string;
  unitsTotal: number;
  unitsActive: number;
  unitsMaintenance: number;
  unitsRetired: number;
  unitIds: string[];
}

export function listEquipmentGrouped(inventoryId: string): Promise<EquipmentStackResponse[]> {
  return apiFetch<EquipmentStackResponse[]>(`/api/inventories/${inventoryId}/equipment?grouped=true`);
}

export function getEquipment(id: string): Promise<EquipmentResponse> {
  return apiFetch<EquipmentResponse>(`/api/equipment/${id}`);
}

export function createEquipment(
  inventoryId: string,
  data: { name: string; reference: string; informations?: string | null; status?: EquipmentStatus },
): Promise<EquipmentResponse> {
  return apiFetch<EquipmentResponse>(`/api/inventories/${inventoryId}/equipment`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateEquipment(
  id: string,
  data: { name?: string | null; reference?: string | null; informations?: string | null; status?: EquipmentStatus | null },
): Promise<EquipmentResponse> {
  return apiFetch<EquipmentResponse>(`/api/equipment/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}

export function deleteEquipment(id: string): Promise<void> {
  return apiFetch<void>(`/api/equipment/${id}`, { method: 'DELETE' });
}
```

- [ ] **Step 7: `frontend/src/api/reservations.ts`**

```ts
import { apiFetch } from './client';

export type ReservationStatus = 'Confirmed' | 'Cancelled';

export interface ReservationResponse {
  id: string;
  equipmentId: string;
  equipmentName: string;
  reference: string;
  userId: string;
  userDisplayName: string;
  startsAt: string;
  endsAt: string;
  status: ReservationStatus;
}

export function listReservations(
  inventoryId: string,
  from: string,
  to: string,
  equipmentId?: string,
): Promise<ReservationResponse[]> {
  const params = new URLSearchParams({ from, to });
  if (equipmentId) params.set('equipmentId', equipmentId);
  return apiFetch<ReservationResponse[]>(`/api/inventories/${inventoryId}/reservations?${params.toString()}`);
}

export function createReservation(
  inventoryId: string,
  data: { equipmentId?: string; name?: string; reference?: string; startsAt: string; endsAt: string },
): Promise<ReservationResponse> {
  return apiFetch<ReservationResponse>(`/api/inventories/${inventoryId}/reservations`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function cancelReservation(id: string): Promise<ReservationResponse> {
  return apiFetch<ReservationResponse>(`/api/reservations/${id}/cancel`, { method: 'POST' });
}
```

- [ ] **Step 8: `frontend/src/api/invitations.ts`**

```ts
import { apiFetch } from './client';

export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';
export type InvitableRole = 'Admin' | 'Member';

export interface InvitationResponse {
  id: string;
  inventoryId: string;
  inventoryName: string;
  invitedUserId: string;
  invitedByUserId: string;
  role: InvitableRole;
  status: InvitationStatus;
  createdAt: string;
}

export function listInventoryInvitations(inventoryId: string): Promise<InvitationResponse[]> {
  return apiFetch<InvitationResponse[]>(`/api/inventories/${inventoryId}/invitations`);
}

export function createInvitation(inventoryId: string, userCode: string, role: InvitableRole): Promise<InvitationResponse> {
  return apiFetch<InvitationResponse>(`/api/inventories/${inventoryId}/invitations`, {
    method: 'POST',
    body: JSON.stringify({ userCode, role }),
  });
}

export function listMyInvitations(): Promise<InvitationResponse[]> {
  return apiFetch<InvitationResponse[]>('/api/invitations');
}

export function acceptInvitation(id: string): Promise<void> {
  return apiFetch<void>(`/api/invitations/${id}/accept`, { method: 'POST' });
}

export function declineInvitation(id: string): Promise<void> {
  return apiFetch<void>(`/api/invitations/${id}/decline`, { method: 'POST' });
}

export function revokeInvitation(id: string): Promise<void> {
  return apiFetch<void>(`/api/invitations/${id}`, { method: 'DELETE' });
}
```

- [ ] **Step 9: `frontend/src/api/members.ts`**

```ts
import { apiFetch } from './client';
import type { MembershipRole } from './inventories';

export interface MemberResponse {
  userId: string;
  displayName: string;
  userCode: string;
  role: MembershipRole;
  joinedAt: string;
}

export function listMembers(inventoryId: string): Promise<MemberResponse[]> {
  return apiFetch<MemberResponse[]>(`/api/inventories/${inventoryId}/members`);
}

export function updateMemberRole(inventoryId: string, userId: string, role: 'Admin' | 'Member'): Promise<void> {
  return apiFetch<void>(`/api/inventories/${inventoryId}/members/${userId}`, {
    method: 'PATCH',
    body: JSON.stringify({ role }),
  });
}

export function removeMember(inventoryId: string, userId: string): Promise<void> {
  return apiFetch<void>(`/api/inventories/${inventoryId}/members/${userId}`, { method: 'DELETE' });
}
```

- [ ] **Step 10: Run — expect PASS**

Run: `cd frontend && npm test`. Expected: all `client.test.ts` tests passing, 0 failures. Also run `npx tsc -b --noEmit` (or `npm run build` if faster to verify) to confirm every file in this task type-checks cleanly.

- [ ] **Step 11: STOP for review**

Do not commit. Report files created, test output, and type-check result. Wait for the user.

---

## Task 3: Auth, layout, and shared form primitives

**Files:**
- Create: `frontend/src/auth/AuthContext.tsx`, `frontend/src/auth/ProtectedRoute.tsx`
- Create: `frontend/src/components/forms/TextField.tsx`, `frontend/src/components/forms/Button.tsx`, `frontend/src/components/forms/ErrorText.tsx`
- Create: `frontend/src/components/layout/NavBar.tsx`, `frontend/src/components/layout/AppLayout.tsx`
- Create: `frontend/src/pages/LoginPage.tsx`, `frontend/src/pages/RegisterPage.tsx`
- Modify: `frontend/src/App.tsx` (real routing, replaces Task 1's placeholder)
- Test: `frontend/src/auth/AuthContext.test.tsx`

**Interfaces:**
- Consumes: `getToken`/`setToken`/`clearToken`/`ApiError` (Task 2, `api/client.ts`), `login`/`register` (Task 2, `api/auth.ts`), `getMe`/`UserResponse` (Task 2, `api/users.ts`).
- Produces: `AuthProvider`, `useAuth()` returning `{ user, loading, login, register, logout }`, `ProtectedRoute` (a React Router layout route element gating everything under it), `TextField`/`Button`/`ErrorText` (used by every later form), `NavBar`/`AppLayout` (used by every later authenticated page) — all consumed by Tasks 4-7.

- [ ] **Step 1: `frontend/src/components/forms/TextField.tsx`**

```tsx
interface TextFieldProps {
  label: string;
  type?: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  placeholder?: string;
}

export function TextField({ label, type = 'text', value, onChange, required, placeholder }: TextFieldProps) {
  return (
    <label className="block text-sm text-gray-700">
      {label}
      <input
        type={type}
        required={required}
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-400"
      />
    </label>
  );
}
```

- [ ] **Step 2: `frontend/src/components/forms/Button.tsx`**

```tsx
import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger';

const variantClasses: Record<Variant, string> = {
  primary: 'bg-primary-500 text-white hover:bg-primary-600',
  secondary: 'bg-gray-100 text-gray-800 hover:bg-gray-200',
  danger: 'bg-red-500 text-white hover:bg-red-600',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = 'primary', className = '', ...rest }: ButtonProps) {
  return (
    <button
      className={`rounded px-4 py-2 font-medium transition-colors disabled:opacity-50 ${variantClasses[variant]} ${className}`}
      {...rest}
    />
  );
}
```

(The `transition-colors` class is Tailwind's built-in color-transition utility — a purely functional hover-state indicator, not a decorative animation, consistent with the design spec's "effects only when they give the user a clear indication.")

- [ ] **Step 3: `frontend/src/components/forms/ErrorText.tsx`**

```tsx
export function ErrorText({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <p className="text-sm text-red-600" role="alert">
      {message}
    </p>
  );
}
```

- [ ] **Step 4: `frontend/src/auth/AuthContext.tsx`**

```tsx
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getMe, type UserResponse } from '../api/users';
import { login as apiLogin, register as apiRegister } from '../api/auth';
import { getToken, setToken, clearToken, ApiError } from '../api/client';

interface AuthContextValue {
  user: UserResponse | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!getToken()) {
      setLoading(false);
      return;
    }
    getMe()
      .then(setUser)
      .catch((err) => {
        if (err instanceof ApiError && err.status === 401) clearToken();
        setUser(null);
      })
      .finally(() => setLoading(false));
  }, []);

  async function login(email: string, password: string) {
    const res = await apiLogin(email, password);
    setToken(res.token);
    setUser(await getMe());
  }

  async function register(email: string, password: string, displayName: string) {
    const res = await apiRegister(email, password, displayName);
    setToken(res.token);
    setUser(await getMe());
  }

  function logout() {
    clearToken();
    setUser(null);
  }

  return <AuthContext.Provider value={{ user, loading, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
```

- [ ] **Step 5: `frontend/src/auth/AuthContext.test.tsx`**

```tsx
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';
import { getToken } from '../api/client';

vi.mock('../api/auth', () => ({
  login: vi.fn(),
  register: vi.fn(),
}));
vi.mock('../api/users', () => ({
  getMe: vi.fn(),
}));

import { login as apiLogin } from '../api/auth';
import { getMe } from '../api/users';

function Probe() {
  const { user, loading, login, logout } = useAuth();
  return (
    <div>
      <span data-testid="loading">{String(loading)}</span>
      <span data-testid="user">{user ? user.displayName : 'none'}</span>
      <button onClick={() => login('a@x.io', 'pw')}>login</button>
      <button onClick={logout}>logout</button>
    </div>
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.mocked(apiLogin).mockReset();
  vi.mocked(getMe).mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('AuthProvider', () => {
  it('starts with no user and loading=false when no token is stored', async () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('loading').textContent).toBe('false'));
    expect(screen.getByTestId('user').textContent).toBe('none');
  });

  it('stores the token and hydrates the user on login', async () => {
    vi.mocked(apiLogin).mockResolvedValue({
      token: 'tok-1', userId: 'u1', email: 'a@x.io', displayName: 'Alice', userCode: 'AB12',
    });
    vi.mocked(getMe).mockResolvedValue({
      userId: 'u1', email: 'a@x.io', displayName: 'Alice', userCode: 'AB12', createdAt: '2026-01-01T00:00:00Z',
    });

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('loading').textContent).toBe('false'));

    screen.getByText('login').click();

    await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('Alice'));
    expect(getToken()).toBe('tok-1');
  });

  it('clears the token and user on logout', async () => {
    vi.mocked(apiLogin).mockResolvedValue({
      token: 'tok-1', userId: 'u1', email: 'a@x.io', displayName: 'Alice', userCode: 'AB12',
    });
    vi.mocked(getMe).mockResolvedValue({
      userId: 'u1', email: 'a@x.io', displayName: 'Alice', userCode: 'AB12', createdAt: '2026-01-01T00:00:00Z',
    });

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('loading').textContent).toBe('false'));
    screen.getByText('login').click();
    await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('Alice'));

    screen.getByText('logout').click();

    expect(screen.getByTestId('user').textContent).toBe('none');
    expect(getToken()).toBeNull();
  });
});
```

- [ ] **Step 6: `frontend/src/auth/ProtectedRoute.tsx`**

```tsx
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function ProtectedRoute() {
  const { user, loading } = useAuth();
  if (loading) {
    return <div className="min-h-screen flex items-center justify-center text-primary-700">Chargement...</div>;
  }
  if (!user) {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
```

- [ ] **Step 7: `frontend/src/components/layout/NavBar.tsx`**

```tsx
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';

export function NavBar() {
  const { user, logout } = useAuth();
  return (
    <nav className="bg-primary-100 border-b border-primary-200 px-6 py-3 flex items-center justify-between">
      <Link to="/" className="font-semibold text-primary-800">Locaccessum</Link>
      <div className="flex items-center gap-4 text-sm">
        <Link to="/" className="text-primary-700 hover:text-primary-900">Mes inventaires</Link>
        <Link to="/profile" className="text-primary-700 hover:text-primary-900">{user?.displayName}</Link>
        <button onClick={logout} className="text-primary-700 hover:text-primary-900 underline">Déconnexion</button>
      </div>
    </nav>
  );
}
```

- [ ] **Step 8: `frontend/src/components/layout/AppLayout.tsx`**

```tsx
import { Outlet } from 'react-router-dom';
import { NavBar } from './NavBar';

export function AppLayout() {
  return (
    <div className="min-h-screen bg-primary-50">
      <NavBar />
      <main className="max-w-4xl mx-auto px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
}
```

- [ ] **Step 9: `frontend/src/pages/LoginPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-primary-50">
      <form onSubmit={handleSubmit} className="bg-white rounded-lg shadow p-8 w-full max-w-sm space-y-4">
        <h1 className="text-xl font-semibold text-primary-800">Connexion</h1>
        <ErrorText message={error} />
        <TextField label="Email" type="email" required value={email} onChange={setEmail} />
        <TextField label="Mot de passe" type="password" required value={password} onChange={setPassword} />
        <Button type="submit" disabled={submitting} className="w-full">
          {submitting ? 'Connexion...' : 'Se connecter'}
        </Button>
        <p className="text-sm text-gray-600 text-center">
          Pas de compte ? <Link to="/register" className="text-primary-700 underline">S'inscrire</Link>
        </p>
      </form>
    </div>
  );
}
```

- [ ] **Step 10: `frontend/src/pages/RegisterPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await register(email, password, displayName);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-primary-50">
      <form onSubmit={handleSubmit} className="bg-white rounded-lg shadow p-8 w-full max-w-sm space-y-4">
        <h1 className="text-xl font-semibold text-primary-800">Inscription</h1>
        <ErrorText message={error} />
        <TextField label="Nom affiché" required value={displayName} onChange={setDisplayName} />
        <TextField label="Email" type="email" required value={email} onChange={setEmail} />
        <TextField label="Mot de passe (8 caractères min.)" type="password" required value={password} onChange={setPassword} />
        <Button type="submit" disabled={submitting} className="w-full">
          {submitting ? 'Inscription...' : "S'inscrire"}
        </Button>
        <p className="text-sm text-gray-600 text-center">
          Déjà un compte ? <Link to="/login" className="text-primary-700 underline">Se connecter</Link>
        </p>
      </form>
    </div>
  );
}
```

- [ ] **Step 11: `frontend/src/App.tsx`** (replaces Task 1's placeholder)

```tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import DashboardPage from './pages/DashboardPage';
import ProfilePage from './pages/ProfilePage';
import InventoryDetailPage from './pages/InventoryDetailPage';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/profile" element={<ProfilePage />} />
              <Route path="/inventories/:id" element={<InventoryDetailPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
```

Note: `DashboardPage`, `ProfilePage`, `InventoryDetailPage` don't exist yet — Task 4 creates `DashboardPage`/`ProfilePage`, Task 5 creates `InventoryDetailPage`. This step will not type-check/build until those exist. That's expected and fine — Step 12 below only requires `npm test` (which only runs `client.test.ts` and `AuthContext.test.tsx`, neither of which imports `App.tsx`) to pass; a full `npm run build` isn't required until Task 4 supplies the missing pages. Do not create stub versions of `DashboardPage`/`ProfilePage`/`InventoryDetailPage` — leave `App.tsx` exactly as above and let the next tasks fill the gap.

- [ ] **Step 12: Run — expect PASS**

Run: `cd frontend && npm test`. Expected: all tests from Task 2 + this task's new `AuthContext.test.tsx` passing, 0 failures.

- [ ] **Step 13: STOP for review**

Do not commit. Report files created/modified and test output (note that a full `npm run build` is expected to fail at this point, per Step 11's note — that's not a defect). Wait for the user.

---

## Task 4: Dashboard, profile, and received invitations

**Files:**
- Create: `frontend/src/pages/DashboardPage.tsx`, `frontend/src/pages/ProfilePage.tsx`

**Interfaces:**
- Consumes: `listInventories`/`createInventory`/`InventoryListItemResponse` (Task 2), `listMyInvitations`/`acceptInvitation`/`declineInvitation`/`InvitationResponse` (Task 2), `getMe` (already used by `AuthContext`, not called again here — `useAuth()`'s `user` is used instead), `TextField`/`Button`/`ErrorText` (Task 3).
- Produces: nothing new consumed by later tasks (leaf pages), but completes `App.tsx`'s routing so `npm run build` succeeds for the first time.

- [ ] **Step 1: `frontend/src/pages/DashboardPage.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  listInventories,
  createInventory,
  type InventoryListItemResponse,
} from '../api/inventories';
import {
  listMyInvitations,
  acceptInvitation,
  declineInvitation,
  type InvitationResponse,
} from '../api/invitations';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function DashboardPage() {
  const [inventories, setInventories] = useState<InventoryListItemResponse[]>([]);
  const [invitations, setInvitations] = useState<InvitationResponse[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  async function refresh() {
    try {
      const [inv, invites] = await Promise.all([listInventories(), listMyInvitations()]);
      setInventories(inv);
      setInvitations(invites);
      setLoadError(null);
    } catch {
      setLoadError('Impossible de charger vos données, réessayez.');
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setCreating(true);
    try {
      await createInventory(name, description || null);
      setName('');
      setDescription('');
      await refresh();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setCreating(false);
    }
  }

  async function handleAccept(id: string) {
    await acceptInvitation(id);
    await refresh();
  }

  async function handleDecline(id: string) {
    await declineInvitation(id);
    await refresh();
  }

  return (
    <div className="space-y-8">
      {loadError && <ErrorText message={loadError} />}

      {invitations.length > 0 && (
        <section className="bg-white rounded-lg shadow p-4">
          <h2 className="font-semibold text-primary-800 mb-2">Invitations reçues</h2>
          <ul className="space-y-2">
            {invitations.map((inv) => (
              <li key={inv.id} className="flex items-center justify-between text-sm">
                <span>
                  {inv.inventoryName} — rôle {inv.role}
                </span>
                <span className="flex gap-2">
                  <Button variant="primary" onClick={() => handleAccept(inv.id)}>Accepter</Button>
                  <Button variant="secondary" onClick={() => handleDecline(inv.id)}>Refuser</Button>
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section>
        <h1 className="text-xl font-semibold text-primary-800 mb-4">Mes inventaires</h1>
        <ul className="space-y-2">
          {inventories.map((inv) => (
            <li key={inv.id}>
              <Link
                to={`/inventories/${inv.id}`}
                className="block bg-white rounded-lg shadow p-4 hover:bg-primary-50"
              >
                <span className="font-medium text-primary-800">{inv.name}</span>
                <span className="text-sm text-gray-600 ml-2">
                  ({inv.myRole}, {inv.memberCount} membre{inv.memberCount > 1 ? 's' : ''})
                </span>
              </Link>
            </li>
          ))}
          {inventories.length === 0 && <p className="text-sm text-gray-600">Aucun inventaire pour le moment.</p>}
        </ul>
      </section>

      <section className="bg-white rounded-lg shadow p-4">
        <h2 className="font-semibold text-primary-800 mb-2">Créer un inventaire</h2>
        <form onSubmit={handleCreate} className="space-y-3">
          <ErrorText message={formError} />
          <TextField label="Nom" required value={name} onChange={setName} />
          <TextField label="Description (optionnelle)" value={description} onChange={setDescription} />
          <Button type="submit" disabled={creating}>{creating ? 'Création...' : 'Créer'}</Button>
        </form>
      </section>
    </div>
  );
}
```

- [ ] **Step 2: `frontend/src/pages/ProfilePage.tsx`**

```tsx
import { useAuth } from '../auth/AuthContext';

export default function ProfilePage() {
  const { user } = useAuth();
  if (!user) return null;

  return (
    <div className="bg-white rounded-lg shadow p-6 max-w-md">
      <h1 className="text-xl font-semibold text-primary-800 mb-4">Mon profil</h1>
      <dl className="space-y-2 text-sm">
        <div>
          <dt className="text-gray-500">Nom affiché</dt>
          <dd className="text-gray-900">{user.displayName}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Email</dt>
          <dd className="text-gray-900">{user.email}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Code utilisateur</dt>
          <dd className="text-gray-900">{user.userCode}</dd>
        </div>
      </dl>
      <p className="text-xs text-gray-500 mt-4">
        Le code utilisateur sert aux autres membres pour vous inviter dans un inventaire.
      </p>
    </div>
  );
}
```

- [ ] **Step 3: Verify the app builds (Task 3's `App.tsx` still references `InventoryDetailPage`, created next task)**

Run: `cd frontend && npm test`. Expected: still passing (no new tests added this task, by design). A full `npm run build` will still fail until Task 5 creates `InventoryDetailPage` — that's expected, same as Task 3's note.

- [ ] **Step 4: STOP for review**

Do not commit. Report files created. Wait for the user.

---

## Task 5: Inventory detail shell + Equipment tab

**Files:**
- Create: `frontend/src/pages/InventoryDetailPage.tsx`, `frontend/src/pages/inventory/EquipmentTab.tsx`

**Interfaces:**
- Consumes: `getInventory`/`InventoryResponse` (Task 2), `listEquipmentGrouped`/`getEquipment`/`createEquipment`/`updateEquipment`/`deleteEquipment`/`EquipmentStackResponse`/`EquipmentResponse` (Task 2), `TextField`/`Button`/`ErrorText` (Task 3).
- Produces: `InventoryDetailPage` (the tab shell — Tasks 6/7 add the `ReservationsTab`/`MembersTab`/`InvitationsTab` this page renders once they exist; until then this task wires only the Equipment tab and stubs the tab-switch UI for the other three as "à venir" placeholders that get replaced, not left permanently — see Step 1).

- [ ] **Step 1: `frontend/src/pages/InventoryDetailPage.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getInventory, type InventoryResponse } from '../api/inventories';
import { EquipmentTab } from './inventory/EquipmentTab';
import { ReservationsTab } from './inventory/ReservationsTab';
import { MembersTab } from './inventory/MembersTab';
import { InvitationsTab } from './inventory/InvitationsTab';
import { ErrorText } from '../components/forms/ErrorText';

type Tab = 'equipment' | 'reservations' | 'members' | 'invitations';

const TABS: { id: Tab; label: string }[] = [
  { id: 'equipment', label: 'Équipement' },
  { id: 'reservations', label: 'Réservations' },
  { id: 'members', label: 'Membres' },
  { id: 'invitations', label: 'Invitations' },
];

export default function InventoryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [inventory, setInventory] = useState<InventoryResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('equipment');

  useEffect(() => {
    if (!id) return;
    getInventory(id)
      .then(setInventory)
      .catch(() => setError("Impossible de charger cet inventaire."));
  }, [id]);

  if (!id) return null;
  if (error) return <ErrorText message={error} />;
  if (!inventory) return <p className="text-primary-700">Chargement...</p>;

  const canManage = inventory.myRole === 'Owner' || inventory.myRole === 'Admin';

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-primary-800">{inventory.name}</h1>
        {inventory.description && <p className="text-sm text-gray-600">{inventory.description}</p>}
      </div>

      <div className="flex gap-1 border-b border-primary-200">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`px-4 py-2 text-sm font-medium ${
              tab === t.id
                ? 'border-b-2 border-primary-600 text-primary-800'
                : 'text-gray-500 hover:text-primary-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'equipment' && <EquipmentTab inventoryId={id} canManage={canManage} />}
      {tab === 'reservations' && <ReservationsTab inventoryId={id} />}
      {tab === 'members' && <MembersTab inventoryId={id} canManage={canManage} isOwner={inventory.myRole === 'Owner'} />}
      {tab === 'invitations' && <InvitationsTab inventoryId={id} canManage={canManage} />}
    </div>
  );
}
```

Note: this file imports `ReservationsTab`/`MembersTab`/`InvitationsTab`, which don't exist until Tasks 6/7. Same pattern as Task 3's `App.tsx` note — leave it as-is, don't stub them; `npm run build` won't fully succeed until Task 7 completes, `npm test` (which doesn't import this page) still passes throughout.

- [ ] **Step 2: `frontend/src/pages/inventory/EquipmentTab.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react';
import {
  listEquipmentGrouped,
  getEquipment,
  createEquipment,
  updateEquipment,
  deleteEquipment,
  type EquipmentStackResponse,
  type EquipmentResponse,
  type EquipmentStatus,
} from '../../api/equipment';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

const STATUS_LABEL: Record<EquipmentStatus, string> = {
  Active: 'Disponible',
  Maintenance: 'En maintenance',
  Retired: 'Retiré',
};

export function EquipmentTab({ inventoryId, canManage }: { inventoryId: string; canManage: boolean }) {
  const [stacks, setStacks] = useState<EquipmentStackResponse[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [units, setUnits] = useState<EquipmentResponse[]>([]);
  const [editing, setEditing] = useState<EquipmentResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [reference, setReference] = useState('');
  const [informations, setInformations] = useState('');
  const [creating, setCreating] = useState(false);

  async function refresh() {
    try {
      setStacks(await listEquipmentGrouped(inventoryId));
      setError(null);
    } catch {
      setError("Impossible de charger l'équipement.");
    }
  }

  useEffect(() => {
    refresh();
  }, [inventoryId]);

  async function toggleExpand(stackKey: string, unitIds: string[]) {
    if (expanded === stackKey) {
      setExpanded(null);
      setUnits([]);
      return;
    }
    const loaded = await Promise.all(unitIds.map((id) => getEquipment(id)));
    setUnits(loaded);
    setExpanded(stackKey);
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setCreating(true);
    try {
      await createEquipment(inventoryId, { name, reference, informations: informations || null });
      setName('');
      setReference('');
      setInformations('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setCreating(false);
    }
  }

  async function handleUpdateStatus(unitId: string, status: EquipmentStatus) {
    await updateEquipment(unitId, { status });
    if (expanded) {
      const unitIds = units.map((u) => u.id);
      const loaded = await Promise.all(unitIds.map((id) => getEquipment(id)));
      setUnits(loaded);
    }
    await refresh();
  }

  async function handleDelete(unitId: string) {
    try {
      await deleteEquipment(unitId);
      setUnits((prev) => prev.filter((u) => u.id !== unitId));
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />

      <ul className="space-y-2">
        {stacks.map((stack) => {
          const stackKey = `${stack.name}::${stack.reference}`;
          return (
            <li key={stackKey} className="bg-white rounded-lg shadow p-4">
              <button
                onClick={() => toggleExpand(stackKey, stack.unitIds)}
                className="w-full flex items-center justify-between text-left"
              >
                <span className="font-medium text-primary-800">
                  {stack.name} <span className="text-gray-500 font-normal">({stack.reference})</span>
                </span>
                <span className="text-sm text-gray-600">
                  {stack.unitsActive} disponible{stack.unitsActive > 1 ? 's' : ''} / {stack.unitsTotal}
                </span>
              </button>
              {expanded === stackKey && (
                <ul className="mt-3 space-y-2 border-t border-primary-100 pt-3">
                  {units.map((unit) => (
                    <li key={unit.id} className="flex items-center justify-between text-sm">
                      <span>
                        {STATUS_LABEL[unit.status]}
                        {unit.informations && <span className="text-gray-500 ml-2">— {unit.informations}</span>}
                      </span>
                      {canManage && (
                        <span className="flex gap-2 items-center">
                          <select
                            value={unit.status}
                            onChange={(e) => handleUpdateStatus(unit.id, e.target.value as EquipmentStatus)}
                            className="border border-gray-300 rounded px-2 py-1 text-xs"
                          >
                            {(['Active', 'Maintenance', 'Retired'] as EquipmentStatus[]).map((s) => (
                              <option key={s} value={s}>{STATUS_LABEL[s]}</option>
                            ))}
                          </select>
                          <Button variant="danger" onClick={() => handleDelete(unit.id)}>Supprimer</Button>
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </li>
          );
        })}
        {stacks.length === 0 && <p className="text-sm text-gray-600">Aucun équipement pour le moment.</p>}
      </ul>

      {canManage && (
        <form onSubmit={handleCreate} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
          <h3 className="font-semibold text-primary-800">Ajouter un équipement</h3>
          <TextField label="Nom" required value={name} onChange={setName} />
          <TextField label="Référence" required value={reference} onChange={setReference} />
          <TextField label="Informations (optionnel)" value={informations} onChange={setInformations} />
          <Button type="submit" disabled={creating}>{creating ? 'Ajout...' : 'Ajouter'}</Button>
        </form>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Verify tests still pass**

Run: `cd frontend && npm test`. Expected: still passing (no new tests this task, by design — `ReservationsTab`/`MembersTab`/`InvitationsTab` don't exist yet so `npm run build` still fails, expected until Task 7).

- [ ] **Step 4: STOP for review**

Do not commit. Report files created. Wait for the user.

---

## Task 6: Reservations tab

**Files:**
- Create: `frontend/src/pages/inventory/ReservationsTab.tsx`

**Interfaces:**
- Consumes: `listReservations`/`createReservation`/`cancelReservation`/`ReservationResponse` (Task 2), `TextField`/`Button`/`ErrorText` (Task 3).
- Produces: nothing consumed by later tasks (leaf tab component, already imported by Task 5's `InventoryDetailPage`).

- [ ] **Step 1: `frontend/src/pages/inventory/ReservationsTab.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react';
import { listReservations, createReservation, cancelReservation, type ReservationResponse } from '../../api/reservations';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

function defaultRange() {
  const from = new Date();
  const to = new Date();
  to.setDate(to.getDate() + 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

export function ReservationsTab({ inventoryId }: { inventoryId: string }) {
  const initial = defaultRange();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [reservations, setReservations] = useState<ReservationResponse[]>([]);
  const [listError, setListError] = useState<string | null>(null);

  const [equipmentName, setEquipmentName] = useState('');
  const [equipmentReference, setEquipmentReference] = useState('');
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function refresh() {
    try {
      const fromIso = new Date(`${from}T00:00:00`).toISOString();
      const toIso = new Date(`${to}T23:59:59`).toISOString();
      setReservations(await listReservations(inventoryId, fromIso, toIso));
      setListError(null);
    } catch {
      setListError('Impossible de charger les réservations, réessayer.');
    }
  }

  useEffect(() => {
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inventoryId, from, to]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setSubmitting(true);
    try {
      await createReservation(inventoryId, {
        name: equipmentName,
        reference: equipmentReference,
        startsAt: new Date(startsAt).toISOString(),
        endsAt: new Date(endsAt).toISOString(),
      });
      setEquipmentName('');
      setEquipmentReference('');
      setStartsAt('');
      setEndsAt('');
      await refresh();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCancel(id: string) {
    await cancelReservation(id);
    await refresh();
  }

  return (
    <div className="space-y-6">
      <div className="flex gap-4 items-end">
        <TextField label="Du" type="date" value={from} onChange={setFrom} />
        <TextField label="Au" type="date" value={to} onChange={setTo} />
      </div>

      <ErrorText message={listError} />

      <ul className="space-y-2">
        {reservations.map((r) => (
          <li key={r.id} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              <span className="font-medium text-primary-800">{r.equipmentName}</span>{' '}
              <span className="text-gray-500">({r.reference})</span> — {r.userDisplayName}
              <br />
              {new Date(r.startsAt).toLocaleString('fr-FR')} → {new Date(r.endsAt).toLocaleString('fr-FR')}
              {r.status === 'Cancelled' && <span className="text-red-600 ml-2">(annulée)</span>}
            </span>
            {r.status === 'Confirmed' && (
              <Button variant="secondary" onClick={() => handleCancel(r.id)}>Annuler</Button>
            )}
          </li>
        ))}
        {reservations.length === 0 && <p className="text-sm text-gray-600">Aucune réservation sur cette période.</p>}
      </ul>

      <form onSubmit={handleCreate} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
        <h3 className="font-semibold text-primary-800">Réserver un équipement</h3>
        <ErrorText message={formError} />
        <TextField label="Nom de l'équipement" required value={equipmentName} onChange={setEquipmentName} />
        <TextField label="Référence" required value={equipmentReference} onChange={setEquipmentReference} />
        <TextField label="Début" type="datetime-local" required value={startsAt} onChange={setStartsAt} />
        <TextField label="Fin" type="datetime-local" required value={endsAt} onChange={setEndsAt} />
        <Button type="submit" disabled={submitting}>{submitting ? 'Réservation...' : 'Réserver'}</Button>
      </form>
    </div>
  );
}
```

- [ ] **Step 2: Verify tests still pass**

Run: `cd frontend && npm test`. Expected: still passing (no new tests this task, by design — `MembersTab`/`InvitationsTab` still missing so `npm run build` still fails, expected until Task 7).

- [ ] **Step 3: STOP for review**

Do not commit. Report files created. Wait for the user.

---

## Task 7: Members and invitations (inventory-scoped) tabs

**Files:**
- Create: `frontend/src/pages/inventory/MembersTab.tsx`, `frontend/src/pages/inventory/InvitationsTab.tsx`

**Interfaces:**
- Consumes: `listMembers`/`updateMemberRole`/`removeMember`/`MemberResponse` (Task 2), `transferOwnership` (Task 2, `api/inventories.ts`), `listInventoryInvitations`/`createInvitation`/`revokeInvitation`/`InvitationResponse` (Task 2), `TextField`/`Button`/`ErrorText` (Task 3).
- Produces: nothing consumed by later tasks — this is the last frontend-feature task. Once this task is done, `App.tsx`'s full route tree resolves and `npm run build` succeeds for the first time.

- [ ] **Step 1: `frontend/src/pages/inventory/MembersTab.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react';
import { listMembers, updateMemberRole, removeMember, type MemberResponse } from '../../api/members';
import { transferOwnership } from '../../api/inventories';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

export function MembersTab({
  inventoryId,
  canManage,
  isOwner,
}: {
  inventoryId: string;
  canManage: boolean;
  isOwner: boolean;
}) {
  const [members, setMembers] = useState<MemberResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [transferTo, setTransferTo] = useState('');
  const [transferring, setTransferring] = useState(false);

  async function refresh() {
    try {
      setMembers(await listMembers(inventoryId));
      setError(null);
    } catch {
      setError('Impossible de charger les membres.');
    }
  }

  useEffect(() => {
    refresh();
  }, [inventoryId]);

  async function handleRoleChange(userId: string, role: 'Admin' | 'Member') {
    try {
      await updateMemberRole(inventoryId, userId, role);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleRemove(userId: string) {
    try {
      await removeMember(inventoryId, userId);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    }
  }

  async function handleTransfer(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setTransferring(true);
    try {
      await transferOwnership(inventoryId, transferTo);
      setTransferTo('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setTransferring(false);
    }
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />
      <ul className="space-y-2">
        {members.map((m) => (
          <li key={m.userId} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              <span className="font-medium text-primary-800">{m.displayName}</span>{' '}
              <span className="text-gray-500">({m.userCode})</span> — {m.role}
            </span>
            {canManage && m.role !== 'Owner' && (
              <span className="flex gap-2 items-center">
                <select
                  value={m.role}
                  onChange={(e) => handleRoleChange(m.userId, e.target.value as 'Admin' | 'Member')}
                  className="border border-gray-300 rounded px-2 py-1 text-xs"
                >
                  <option value="Admin">Admin</option>
                  <option value="Member">Membre</option>
                </select>
                <Button variant="danger" onClick={() => handleRemove(m.userId)}>Retirer</Button>
              </span>
            )}
          </li>
        ))}
      </ul>

      {isOwner && (
        <form onSubmit={handleTransfer} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
          <h3 className="font-semibold text-primary-800">Transférer la propriété</h3>
          <TextField label="ID utilisateur du nouveau propriétaire" required value={transferTo} onChange={setTransferTo} />
          <Button type="submit" variant="danger" disabled={transferring}>
            {transferring ? 'Transfert...' : 'Transférer'}
          </Button>
        </form>
      )}
    </div>
  );
}
```

- [ ] **Step 2: `frontend/src/pages/inventory/InvitationsTab.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react';
import {
  listInventoryInvitations,
  createInvitation,
  revokeInvitation,
  type InvitationResponse,
  type InvitableRole,
} from '../../api/invitations';
import { ApiError } from '../../api/client';
import { TextField } from '../../components/forms/TextField';
import { Button } from '../../components/forms/Button';
import { ErrorText } from '../../components/forms/ErrorText';

export function InvitationsTab({ inventoryId, canManage }: { inventoryId: string; canManage: boolean }) {
  const [invitations, setInvitations] = useState<InvitationResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [userCode, setUserCode] = useState('');
  const [role, setRole] = useState<InvitableRole>('Member');
  const [inviting, setInviting] = useState(false);

  async function refresh() {
    try {
      setInvitations(await listInventoryInvitations(inventoryId));
      setError(null);
    } catch {
      setError('Impossible de charger les invitations.');
    }
  }

  useEffect(() => {
    if (canManage) refresh();
  }, [inventoryId, canManage]);

  async function handleInvite(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setInviting(true);
    try {
      await createInvitation(inventoryId, userCode, role);
      setUserCode('');
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setInviting(false);
    }
  }

  async function handleRevoke(id: string) {
    await revokeInvitation(id);
    await refresh();
  }

  if (!canManage) {
    return <p className="text-sm text-gray-600">Seuls les administrateurs peuvent gérer les invitations.</p>;
  }

  return (
    <div className="space-y-6">
      <ErrorText message={error} />
      <ul className="space-y-2">
        {invitations.map((inv) => (
          <li key={inv.id} className="bg-white rounded-lg shadow p-4 flex items-center justify-between text-sm">
            <span>
              {inv.role} — {inv.status}
            </span>
            {inv.status === 'Pending' && (
              <Button variant="secondary" onClick={() => handleRevoke(inv.id)}>Révoquer</Button>
            )}
          </li>
        ))}
        {invitations.length === 0 && <p className="text-sm text-gray-600">Aucune invitation.</p>}
      </ul>

      <form onSubmit={handleInvite} className="bg-white rounded-lg shadow p-4 space-y-3 max-w-md">
        <h3 className="font-semibold text-primary-800">Inviter quelqu'un</h3>
        <TextField label="Code utilisateur" required value={userCode} onChange={setUserCode} />
        <label className="block text-sm text-gray-700">
          Rôle
          <select
            value={role}
            onChange={(e) => setRole(e.target.value as InvitableRole)}
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2"
          >
            <option value="Member">Membre</option>
            <option value="Admin">Admin</option>
          </select>
        </label>
        <Button type="submit" disabled={inviting}>{inviting ? 'Envoi...' : 'Inviter'}</Button>
      </form>
    </div>
  );
}
```

- [ ] **Step 3: Full build now succeeds**

Run: `cd frontend && npm test && npm run build`. Expected: all tests still passing, and `npm run build` now succeeds for the first time (every page `App.tsx` routes to now exists) — producing `dist/index.html` + assets.

- [ ] **Step 4: STOP for review**

Do not commit. Report files created and the full build output. Wait for the user.

---

## Task 8: Docker integration, README, and manual E2E validation

**Files:**
- Create: `frontend/Dockerfile`, `frontend/.dockerignore`
- Modify: `docker-compose.yml` (add `frontend` service), `README.md` (bilingual "Frontend" section)

**Interfaces:**
- Consumes: everything (the whole app must build and run as a container).
- Produces: a `frontend` container serving the built SPA, joining `postgres`/`api`/`worker` under `docker compose up`.

- [ ] **Step 1: `frontend/Dockerfile`**

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

(`VITE_API_BASE_URL` is baked in at build time, per Vite's static-env-var model — this is fine per the Security Requirements: it's a public base URL, not a secret. `nginx:alpine`'s default config already serves a static SPA correctly for our routes since every route is a client-side route under `/` with no nested static assets colliding — no custom nginx config needed for this simple case; if `npm run build` produces any deep-linking issue during Step 4's manual check (e.g. a hard refresh on `/inventories/x` 404s), that's fine, note it as a known limitation, do not add SPA-fallback nginx config as a fix in this task — it's not required for the manual click-through, which navigates via the app's own links, not hard refreshes.)

- [ ] **Step 2: `frontend/.dockerignore`**

```
node_modules
dist
.env
```

- [ ] **Step 3: Add the `frontend` service to the root `docker-compose.yml`**

Read the current `docker-compose.yml` first (it has `postgres`, `api`, `worker`). Add a fourth service:

```yaml
  frontend:
    build:
      context: ./frontend
      args:
        VITE_API_BASE_URL: http://localhost:8080
    depends_on:
      api:
        condition: service_started
    ports:
      - "5173:80"
    restart: unless-stopped
```

(The build-time `VITE_API_BASE_URL` points at `http://localhost:8080` — the *browser's* view of the API, not the Docker network's `http://api:8080` used by the worker — because this URL is baked into JS that runs in the user's browser, outside the Docker network, unlike the worker's server-to-server calls.)

- [ ] **Step 4: Real compose smoke test with all four services**

From the repo root:
```bash
docker compose up --build -d
sleep 25
docker compose ps
curl -s -w "\nHTTP_STATUS:%{http_code}\n" localhost:5173
curl -s -w "\nHTTP_STATUS:%{http_code}\n" localhost:8080/health
docker compose down
```
Expected: `frontend` container running (no crash-loop), `localhost:5173` returns `200` with HTML containing `<div id="root">`, `/health` still `200`. Do not pass `-v` to `down`.

- [ ] **Step 5: Manual click-through validation**

With the stack running (`docker compose up --build -d`, or `cd frontend && npm run dev` against a locally running backend — whichever is faster to iterate with), open the app in a browser and walk through: register a new user → land on dashboard → create an inventory → add equipment (create two units of the same name+reference to verify stacking shows `unitsTotal: 2`) → reserve one unit → view it in the reservations list → cancel it → go to Members tab (see yourself as Owner) → go to Invitations tab, invite a second test user by their user code (register a second account first to get one) → log in as the second user, see the invitation on the dashboard, accept it → confirm the second user now sees the inventory and appears in Members. Report exactly what was checked and any issue found (fix genuine bugs found here directly, same as any other task — this is the equivalent of the worker plan's Task 8 E2E pass).

- [ ] **Step 6: Write the README section**

Add a new section to `README.md`, in BOTH the French part and the English part (same bilingual pattern as the worker's section), titled "Frontend" / "Frontend". Cover: what it is (Vite + React + TypeScript SPA), how to run it standalone (`cd frontend && cp .env.example .env && npm install && npm run dev`), how it's wired into `docker compose up` (served on port 5173, built with `VITE_API_BASE_URL` baked in), and that it's intentionally simple (pastel-green Tailwind theme, no heavy client-side effects) per this plan's design goals. Also update the top-of-file sentence one more time — it should now say the repo contains backend, worker, **and** frontend, with nothing left unbuilt.

- [ ] **Step 7: STOP for review**

Do not commit. This is the last task of Plan 2. Report the full smoke-test output, the manual click-through results (including anything fixed), and the README diff. Wait for the user.

---

## Self-Review

**1. Spec coverage:** every screen listed in `docs/superpowers/specs/2026-09-08-locaccessum-frontend-design.md`'s Architecture section maps to a task (routes → Tasks 3/4/5, tabs → Tasks 5/6/7, Docker/README → Task 8). All 8 Security Requirements are either directly enforced by the code as written (no `dangerouslySetInnerHTML` anywhere — verified: every dynamic value in every JSX file above is plain `{expr}` interpolation, never bypassed; single `localStorage` key via `client.ts`'s helpers, used consistently by `AuthContext` — no other file touches `localStorage`; `apiFetch`'s error handling only ever surfaces `detail`/`errors`/`title`, never a raw body; role-based UI hiding — `canManage`/`isOwner` — is explicitly documented as UX-only in Task 5/7's interfaces, with every privileged action still routed through the backend, which independently enforces it) or called out explicitly for the final review to verify end-to-end (Task 8 is where the whole build gets checked for secrets in the bundle, matching the spec's Testing Strategy section).

**2. Placeholder scan:** no TBD/TODO. Two intentional "not yet importable" notes exist (Task 3's `App.tsx` referencing pages Task 4/5 haven't created yet, Task 5's `InventoryDetailPage` referencing tabs Task 6/7 haven't created yet) — these are explicitly flagged as expected, temporary, and self-resolving by the time the referenced task lands, not vague hand-waves; each says exactly which later task resolves it.

**3. Type consistency:** `InventoryResponse.myRole`/`MembershipRole` (Task 2) is the same type used by `InventoryDetailPage.canManage`/`isOwner` (Task 5) and `MembersTab.role`/`updateMemberRole` (Task 7). `EquipmentStackResponse.unitIds` (Task 2) is consumed by `EquipmentTab.toggleExpand` (Task 5) exactly as declared. `ApiError` (Task 2) is imported and pattern-matched identically (`err instanceof ApiError ? err.message : ...`) in every form across Tasks 3/4/5/6/7 — one consistent error-handling idiom throughout, nothing bespoke per page.

Plan ready.
