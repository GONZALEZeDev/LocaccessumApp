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
