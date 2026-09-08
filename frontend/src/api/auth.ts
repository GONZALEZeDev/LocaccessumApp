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
