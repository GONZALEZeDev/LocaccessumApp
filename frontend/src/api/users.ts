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
