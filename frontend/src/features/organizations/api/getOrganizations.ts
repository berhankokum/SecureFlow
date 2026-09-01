import { apiClient } from "@/lib/api/apiClient";
import type { Organization } from "./types";

export function getOrganizations(): Promise<Organization[]> {
  return apiClient.get<Organization[]>("/organizations");
}
