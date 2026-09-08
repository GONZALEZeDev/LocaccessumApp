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
