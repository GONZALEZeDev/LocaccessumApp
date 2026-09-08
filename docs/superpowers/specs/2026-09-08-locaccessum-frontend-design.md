# Locaccessum Frontend (Plan 2) — Design

**Status:** Approved for planning
**Relates to:** `docs/superpowers/specs/2026-09-08-locaccessum-design.md` (master spec, §1-6 describe the backend this frontend consumes), `docs/superpowers/plans/2026-09-08-locaccessum-backend.md` (Plan 1, done), `docs/superpowers/plans/2026-09-08-locaccessum-worker.md` (Plan 3, done)

## Goal

A simple, functional React frontend that lets a user navigate the entire existing Locaccessum backend without getting lost: register/login, manage inventories, manage equipment (including stacked/grouped identical items), reserve equipment, manage members and invitations. Visual design is intentionally minimal — light pastel green tones, no bespoke art direction, no heavy client-side effects. **Resource and verification effort for this plan is deliberately lower than Plans 1 and 3** (backend and worker), except for one dimension: frontend security, which gets full attention (see Security Requirements below).

## Non-Goals (explicit, to prevent scope creep)

- No custom visual identity / branding beyond a pastel-green color palette.
- No animation-heavy or resource-heavy UI effects. Any transition/effect used must be purely functional (e.g., a loading spinner, a subtle hover state indicating something is clickable) — never decorative.
- No global state management library (Redux, Zustand, etc.) — a single React context for the authenticated user is enough at this scale.
- No client-side caching/sync layer (React Query, SWR) — plain `fetch` + component state.
- No exhaustive automated test suite mirroring Plans 1/3's rigor. Testing is reduced in breadth (see Testing Strategy).
- No backend changes. The frontend consumes the existing API exactly as built; if a genuine backend gap is found, it's ledgered and raised to the user, not silently worked around.
- No graphical calendar widget for reservations — a simple filterable list view (by equipment / date range) is sufficient, matching `GET /api/inventories/{id}/reservations` (calendar query params `from`/`to`/`equipmentId`).

## Tech Stack

- **Vite + React + TypeScript** — the backend's default CORS origin (`Cors__FrontOrigin`, default `http://localhost:5173`) already assumes Vite's default dev port.
- **React Router** for client-side routing.
- **Tailwind CSS** for styling — utility classes, a small custom theme (pastel green palette + neutral grays), no component library on top of it.
- **Native `fetch`** via a thin API client module (one file per backend resource: `auth`, `inventories`, `equipment`, `reservations`, `invitations`, `members`, `users`), following the same pattern already used in `worker/src/apiClient.ts` (typed request/response, throws a descriptive error on non-OK responses).
- **JWT in `localStorage`**, sent via `Authorization: Bearer <token>` header. No backend changes needed (backend already issues JWTs on login/register per `docs/superpowers/specs/2026-09-08-locaccessum-design.md`).

## Architecture

### Routes

| Route | Purpose |
|---|---|
| `/login`, `/register` | Auth forms — unauthenticated only (redirect to `/` if already logged in) |
| `/` | Dashboard: list of my inventories, create-inventory button |
| `/inventories/:id` | Inventory detail, with simple tab/section navigation: Equipment / Reservations / Members / Invitations |
| `/profile` | Current user info (`GET /api/users/me`) |

All routes except `/login`/`/register` are protected: an auth guard redirects to `/login` if no valid token is present. A persistent top navigation bar (dashboard link, profile link, logout) is shown whenever authenticated.

### Inventory detail sections (not separate routes — client-side tabs within `/inventories/:id`)

- **Equipment**: grouped/stacked list (identical equipment sharing a name, differentiated by reference — mirrors the backend's `grouped=true` list mode), create/edit/delete forms. Only inventory members with sufficient role can create/edit/delete (the frontend hides actions the user's role can't perform, but this is a UX nicety only — see Security Requirements: the backend remains the sole enforcement point).
- **Reservations**: list of reservations filtered by date range and optionally by equipment, create-reservation form (equipment + start/end), cancel action on my own reservations.
- **Members**: list of inventory members and roles; owner/admin can change roles or remove members; owner can transfer ownership.
- **Invitations**: invitations tied to this inventory (create/list/revoke, visible to owner/admin) — separate from the user's own received invitations, which live under `/profile` or a small "My invitations" section reachable from the dashboard (accept/decline `POST /api/invitations/{id}/accept|decline`).

### File structure

```
frontend/
  src/
    pages/            (one file per route above)
    components/
      layout/          (nav bar, protected-route wrapper, page shell)
      forms/           (shared form primitives: text input, select, error display)
    api/
      client.ts        (shared fetch wrapper: base URL, auth header injection, error handling)
      auth.ts, inventories.ts, equipment.ts, reservations.ts, invitations.ts, members.ts, users.ts
    auth/
      AuthContext.tsx   (current user + token, login/logout, persists to localStorage)
      ProtectedRoute.tsx
    styles/
      (Tailwind config: pastel green theme tokens)
    App.tsx, main.tsx
  index.html, vite.config.ts, tailwind.config.ts, package.json, tsconfig.json, .env.example, Dockerfile, .dockerignore
```

### Data flow

`AuthContext` holds the current user + JWT (hydrated from `localStorage` on load, validated against `GET /api/users/me` — if that call fails with 401, the stored token is cleared and the user is redirected to `/login`). Every API call goes through `api/client.ts`, which attaches the `Authorization` header when a token is present and throws a typed error (with the HTTP status) on non-OK responses, letting each page/component show a simple inline error message. No component talks to `fetch` directly.

### Error handling

Inline error text under forms for validation/action failures (surfacing the backend's error message where safe — see Security Requirements on what's safe to surface). A small non-blocking banner for list-loading failures (e.g., "Impossible de charger les réservations, réessayer"). No toast library, no elaborate notification system.

## Security Requirements (the priority of this plan)

The frontend must introduce **zero new attack surface** beyond what the backend already accepts. Concretely, binding requirements for every task in the implementation plan:

1. **No secrets in the frontend bundle.** Only `VITE_API_BASE_URL` (a public value, not a secret) is baked into the build via Vite env vars. No API keys, no internal service credentials, nothing resembling `InternalApiKey` ever appears in frontend code, `.env.example`, or the built bundle — the frontend only ever talks to the public API surface, never the internal worker endpoints.
2. **XSS prevention.** No use of `dangerouslySetInnerHTML` anywhere. All user-supplied and backend-returned text (equipment names, display names, inventory names, etc.) is rendered through normal React JSX text interpolation, which auto-escapes — this must never be bypassed. No `eval`, no dynamic `new Function`, no unsanitized injection into the DOM outside React's own rendering.
3. **JWT handling.** The token lives only in `localStorage` under one clearly-named key; it is never logged to the console, never sent to any endpoint other than this backend's own API, and is cleared on logout and on any 401 response. No token or user PII is written into URL query strings (which can leak via browser history, referrer headers, or logs).
4. **Frontend authorization is UX only, never enforcement.** Role-based UI hiding (e.g., hiding a "delete" button from a non-admin) is a convenience, not a security boundary — every privileged action's real gate is the backend's own authorization check (already implemented in Plan 1). The plan must not introduce any client-side-only permission logic that could be mistaken for real access control, and every task's review must confirm the corresponding backend endpoint still independently enforces the check.
5. **No sensitive data over-fetching.** Pages request only the fields/endpoints they need; the app must not, for convenience, fetch and hold in memory more user or inventory data than the current view requires (e.g., don't fetch the full member list with emails when only names are displayed elsewhere).
6. **Error messages don't leak internals.** Displayed error text is limited to what the backend's API already returns as a client-facing message (e.g., "Invalid credentials", "Reservation conflict") — the frontend never displays raw stack traces, internal exception messages, or any backend response field not intended as user-facing (if the backend ever returns something that looks like an internal detail, the frontend truncates to a generic message and that's ledgered as a backend-side finding, not silently displayed).
7. **Dependency hygiene.** Only well-known, actively maintained packages (React, React Router, Tailwind, Vite, TypeScript, Vitest if any tests are added) — no small/obscure npm packages pulled in for minor convenience, minimizing supply-chain surface.
8. **CORS/env correctness.** `.env.example` documents `VITE_API_BASE_URL` with a safe local default; the frontend never hardcodes a production API URL.

## Testing Strategy (reduced scope, by design)

- **No per-component unit test suite.** Given the intentionally simple UI and the user's explicit resource constraint, individual components/pages are not unit-tested exhaustively.
- **A small number of targeted tests are still worth writing** where a real bug would be easy to introduce silently: the `api/client.ts` fetch wrapper (auth header injection, error-on-non-OK), and `AuthContext`'s token persistence/clear-on-401 logic. Vitest + React Testing Library, matching the worker's existing tooling choice.
- **Task review during implementation is lighter than Plans 1/3**: single review pass per task (cheap/mid-tier model), fix loop only for Critical/Important findings, no automated fix-loop escalation ceremony beyond what's needed.
- **The final whole-plan review is still mandatory and is explicitly security-focused**: it must independently verify every item in the Security Requirements section above (grep for `dangerouslySetInnerHTML`/`eval`, confirm no secrets in the built bundle via `npm run build` + inspecting `dist/`, spot-check that role-based UI hiding isn't mistaken for real enforcement, confirm error displays don't leak internals). This mirrors the security-focused final pass already proven useful in Plan 3 (the worker's XSS finding).
- **Manual verification**: a real click-through of every route/flow (register → login → create inventory → add equipment → reserve → invite a second user → accept invitation → manage members → logout) replaces an automated end-to-end test suite for this plan.

## Deployment

- `frontend/Dockerfile`: multi-stage build (Vite build → static files served by a lightweight server, e.g., `nginx:alpine` or Node's `serve`), following the same pattern as the worker's Dockerfile (multi-stage, non-root, minimal final image).
- Added as a fourth service (`frontend`) in the root `docker-compose.yml`, alongside `postgres`/`api`/`worker`.
- `frontend/.env.example` with `VITE_API_BASE_URL` documented.
- README gets a bilingual (FR/EN) "Frontend" section, following the pattern already established for the worker section.

## Process (inherited, unchanged)

All standing preferences from Plans 1/3 apply unchanged: **no autonomous commits** — every task's changes are left uncommitted for the user to review and commit themselves, following the same stop-after-each-task pattern used throughout Plan 3. Execution via `superpowers:subagent-driven-development`, adapted for the no-commit constraint exactly as documented in this plan's SDD workspace ledger (to be created at `.superpowers/sdd/<plan-file-basename>/progress.md`), scaled down per this spec's reduced verification requirements (single review pass, cheap/mid-tier models for implementation and review by default, escalating to a more capable model only for the security-focused final review and for any Critical/Important finding's fix loop).
