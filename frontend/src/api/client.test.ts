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
