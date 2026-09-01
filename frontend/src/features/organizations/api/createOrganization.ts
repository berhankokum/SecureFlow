import { apiClient } from "@/lib/api/apiClient";
import type { CreateOrganizationPayload, Organization } from "./types";

export function createOrganization(payload: CreateOrganizationPayload): Promise<Organization> {
  return apiClient.post<Organization>("/organizations", payload);
}
