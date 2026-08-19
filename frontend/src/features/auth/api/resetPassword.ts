import { apiClient } from "@/lib/api/apiClient";
import type { ResetPasswordPayload } from "./types";

export function resetPassword(payload: ResetPasswordPayload): Promise<void> {
  return apiClient.post<void>("/auth/reset-password", payload);
}
