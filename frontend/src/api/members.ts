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
