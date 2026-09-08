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

type UnauthorizedListener = () => void;
const unauthorizedListeners = new Set<UnauthorizedListener>();

/** Register a callback invoked whenever any apiFetch call gets a 401. Returns an unsubscribe function. */
export function onUnauthorized(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

function notifyUnauthorized(): void {
  clearToken();
  unauthorizedListeners.forEach((listener) => listener());
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
    if (res.status === 401) {
      notifyUnauthorized();
    }

    let message = `Une erreur est survenue (${res.status}).`;
    let code: string | undefined;
    if (res.status >= 500) {
      // Never surface a 5xx body verbatim, even if it happens to be JSON — always generic.
      throw new ApiError(res.status, message, code);
    }
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
