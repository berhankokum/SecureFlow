import { apiClient } from "@/lib/api/apiClient";
import type { RegisterPayload } from "./types";

export function register(payload: RegisterPayload): Promise<void> {
  return apiClient.post<void>("/auth/register", payload);
}
