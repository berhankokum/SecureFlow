import { apiClient } from "@/lib/api/apiClient";
import type { LoginPayload } from "./types";

export function login(payload: LoginPayload): Promise<void> {
  return apiClient.post<void>("/auth/login", payload);
}
