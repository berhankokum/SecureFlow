import { apiClient } from "@/lib/api/apiClient";
import type { ForgotPasswordPayload } from "./types";

export function forgotPassword(payload: ForgotPasswordPayload): Promise<void> {
  return apiClient.post<void>("/auth/forgot-password", payload);
}
