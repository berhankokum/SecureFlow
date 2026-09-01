import { apiClient } from "@/lib/api/apiClient";

export function removeMember(organizationId: string, userId: string): Promise<void> {
  return apiClient.delete(`/organizations/${organizationId}/members/${userId}`);
}
