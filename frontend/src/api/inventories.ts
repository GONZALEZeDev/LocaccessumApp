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
