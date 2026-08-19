import { apiClient } from "@/lib/api/apiClient";

export function logout(): Promise<void> {
  return apiClient.post<void>("/auth/logout");
}
