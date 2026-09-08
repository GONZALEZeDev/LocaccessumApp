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

    await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('none'));
    expect(getToken()).toBeNull();
  });
});
