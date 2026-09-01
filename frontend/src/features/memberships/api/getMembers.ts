import { apiClient } from "@/lib/api/apiClient";
import type { OrganizationMember } from "./types";

export function getMembers(organizationId: string): Promise<OrganizationMember[]> {
  return apiClient.get(`/organizations/${organizationId}/members`);
}
