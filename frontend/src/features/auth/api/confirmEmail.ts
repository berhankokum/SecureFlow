import { apiClient } from "@/lib/api/apiClient";
import type { ConfirmEmailPayload } from "./types";

export function confirmEmail(payload: ConfirmEmailPayload): Promise<void> {
  return apiClient.post<void>("/auth/confirm-email", payload);
}
