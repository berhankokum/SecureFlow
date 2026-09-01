import { apiClient } from "@/lib/api/apiClient";
import type { ChangeMemberRolePayload } from "./types";

export function changeMemberRole(
  organizationId: string,
  userId: string,
  payload: ChangeMemberRolePayload,
): Promise<void> {
  return apiClient.patch(`/organizations/${organizationId}/members/${userId}`, payload);
}
